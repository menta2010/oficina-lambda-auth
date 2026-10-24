using System.Text.Json;

namespace Oficina.Auth.Lambda;

/// <summary>
/// Log estruturado em JSON (uma linha por evento) no stdout, que a Lambda envia ao
/// CloudWatch e ao New Relic. O correlationId permite seguir a mesma requisicao
/// do API Gateway ate a API principal.
/// </summary>
public static class LogJson
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Action<string> Saida { get; set; } = Console.WriteLine;

    public static void Info(string mensagem, string correlationId, object? dados = null)
        => Escrever("INFO", mensagem, correlationId, dados);

    public static void Erro(string mensagem, string correlationId, Exception ex)
        => Escrever("ERROR", mensagem, correlationId, new { erro = ex.GetType().Name, detalhe = ex.Message });

    private static void Escrever(string nivel, string mensagem, string correlationId, object? dados)
        => Saida(JsonSerializer.Serialize(new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            level = nivel,
            service = "oficina-lambda-auth",
            message = mensagem,
            correlationId,
            dados
        }, Opcoes));
}
