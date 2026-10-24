using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Npgsql;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Oficina.Auth.Lambda;

/// <summary>
/// Ponto de entrada da Lambda de autenticacao (API Gateway HTTP API, payload 2.0):
///   POST /auth/cpf   {"cpf": "..."}                  -> JWT do cliente (role Cliente)
///   POST /auth/login {"usuario": "...", "senha": "..."} -> JWT do funcionario (Admin | Atendente)
/// </summary>
public class Function
{
    public const string HeaderCorrelacao = "x-correlation-id";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Inicializado uma vez por container (cold start) e reaproveitado nas invocacoes seguintes.
    // Se a inicializacao falhar (ex.: Secrets Manager indisponivel), a proxima invocacao tenta de novo.
    private static Task<CasosDeUso>? _inicializacao;

    private readonly Func<Task<CasosDeUso>> _casosDeUso;

    public Function() => _casosDeUso = ObterCasosDeUsoPadrao;

    internal Function(AutenticacaoCpf cpf, AutenticacaoFuncionario? funcionario = null)
        => _casosDeUso = () => Task.FromResult(new CasosDeUso(cpf, funcionario!));

    internal sealed record CasosDeUso(AutenticacaoCpf Cpf, AutenticacaoFuncionario Funcionario);

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var correlationId = ObterCorrelationId(request);
        var cronometro = Stopwatch.StartNew();
        var rota = EhLoginFuncionario(request) ? "login" : "cpf";

        try
        {
            var corpo = LerCorpo(request);
            if (corpo is null)
                return Responder(400, new { erro = "Corpo invalido. Envie um JSON." }, correlationId);

            var casos = await _casosDeUso();
            ResultadoAutenticacao resultado;
            string? identificacaoLog;

            if (rota == "login")
            {
                var req = Desserializar<RequisicaoLogin>(corpo);
                resultado = await casos.Funcionario.AutenticarAsync(req?.Usuario, req?.Senha, CancellationToken.None);
                identificacaoLog = req?.Usuario;
            }
            else
            {
                var req = Desserializar<RequisicaoCpf>(corpo);
                if (req?.Cpf is null)
                    return Responder(400, new { erro = "Corpo invalido. Envie {\"cpf\": \"...\"}." }, correlationId);
                resultado = await casos.Cpf.AutenticarAsync(req.Cpf, CancellationToken.None);
                identificacaoLog = Cpf.Mascarar(req.Cpf);
            }

            LogJson.Info("autenticacao", correlationId, new
            {
                rota,
                evento = resultado.Evento,
                statusCode = resultado.StatusCode,
                identificacao = identificacaoLog,
                duracaoMs = cronometro.ElapsedMilliseconds
            });
            return Responder(resultado.StatusCode, resultado.Corpo, correlationId);
        }
        catch (Exception ex)
        {
            LogJson.Erro("falha_autenticacao", correlationId, ex);
            return Responder(500, new { erro = "Erro interno ao autenticar." }, correlationId);
        }
    }

    private static bool EhLoginFuncionario(APIGatewayHttpApiV2ProxyRequest request)
        => (request.RouteKey ?? request.RawPath ?? string.Empty).EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase);

    private static string ObterCorrelationId(APIGatewayHttpApiV2ProxyRequest request)
    {
        if (request.Headers is not null)
        {
            foreach (var (nome, valor) in request.Headers)
                if (nome.Equals(HeaderCorrelacao, StringComparison.OrdinalIgnoreCase) && CorrelacaoValida(valor))
                    return valor;
        }
        return request.RequestContext?.RequestId ?? Guid.NewGuid().ToString();
    }

    // Valor vindo do cliente: aceito apenas se curto e com caracteres seguros para log/header
    internal static bool CorrelacaoValida(string? valor)
        => !string.IsNullOrWhiteSpace(valor)
           && valor.Length <= 100
           && valor.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    private static string? LerCorpo(APIGatewayHttpApiV2ProxyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body)) return null;
        return request.IsBase64Encoded
            ? Encoding.UTF8.GetString(Convert.FromBase64String(request.Body))
            : request.Body;
    }

    private static T? Desserializar<T>(string corpo) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(corpo, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static APIGatewayHttpApiV2ProxyResponse Responder(int status, object corpo, string correlationId) => new()
    {
        StatusCode = status,
        Body = JsonSerializer.Serialize(corpo, Json),
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            [HeaderCorrelacao] = correlationId
        }
    };

    private static Task<CasosDeUso> ObterCasosDeUsoPadrao()
    {
        var atual = _inicializacao;
        if (atual is null || atual.IsFaulted || atual.IsCanceled)
            _inicializacao = atual = CriarCasosDeUsoAsync();
        return atual;
    }

    private static async Task<CasosDeUso> CriarCasosDeUsoAsync()
    {
        var (connectionString, jwt) = await Configuracao.CarregarAsync();
        var repositorio = new PostgresAutenticacaoRepository(NpgsqlDataSource.Create(connectionString));
        var emissor = new JwtEmissor(jwt!);
        return new CasosDeUso(new AutenticacaoCpf(repositorio, emissor), new AutenticacaoFuncionario(repositorio, emissor));
    }

    private sealed record RequisicaoCpf(string? Cpf);
    private sealed record RequisicaoLogin(string? Usuario, string? Senha);
}
