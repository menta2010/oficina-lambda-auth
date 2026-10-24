using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using static Oficina.Auth.Lambda.Tests.AutenticacaoCpfTests;

namespace Oficina.Auth.Lambda.Tests;

public class FunctionTests
{
    private readonly List<string> _logs = new();

    public FunctionTests() => LogJson.Saida = _logs.Add;

    private static Function Criar() => new(new AutenticacaoCpf(
        new ClientesFake().Com(CpfValido, new(Guid.NewGuid(), "Maria", Ativo: true)),
        new JwtEmissor(Jwt)));

    private static APIGatewayHttpApiV2ProxyRequest Requisicao(string? corpo, bool base64 = false, string? correlationId = null) => new()
    {
        Body = base64 && corpo is not null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(corpo)) : corpo,
        IsBase64Encoded = base64,
        Headers = correlationId is null ? new Dictionary<string, string>() : new() { ["X-Correlation-Id"] = correlationId },
        RequestContext = new() { RequestId = "req-123" }
    };

    [Fact]
    public async Task Autentica_e_devolve_correlation_id_do_cliente()
    {
        var resp = await Criar().FunctionHandler(Requisicao("{\"cpf\":\"529.982.247-25\"}", correlationId: "abc-1"), new TestLambdaContext());

        resp.StatusCode.Should().Be(200);
        resp.Headers[Function.HeaderCorrelacao].Should().Be("abc-1");
        JsonDocument.Parse(resp.Body).RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Aceita_corpo_em_base64_e_usa_request_id_como_correlacao()
    {
        var resp = await Criar().FunctionHandler(Requisicao("{\"cpf\":\"52998224725\"}", base64: true), new TestLambdaContext());

        resp.StatusCode.Should().Be(200);
        resp.Headers[Function.HeaderCorrelacao].Should().Be("req-123");
    }

    [Fact]
    public async Task CorrelationId_Inseguro_UsaORequestIdDoGateway()
    {
        var resp = await Criar().FunctionHandler(
            Requisicao("{\"cpf\":\"52998224725\"}", correlationId: "<script>x</script>"), new TestLambdaContext());

        resp.Headers[Function.HeaderCorrelacao].Should().Be("req-123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-json")]
    [InlineData("{\"documento\":\"52998224725\"}")]
    public async Task Corpo_invalido_retorna_400(string? corpo)
    {
        var resp = await Criar().FunctionHandler(Requisicao(corpo), new TestLambdaContext());

        resp.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Log_e_json_estruturado_sem_cpf_em_claro()
    {
        await Criar().FunctionHandler(Requisicao("{\"cpf\":\"529.982.247-25\"}", correlationId: "abc-2"), new TestLambdaContext());

        var log = JsonDocument.Parse(_logs.Single()).RootElement;
        log.GetProperty("correlationId").GetString().Should().Be("abc-2");
        log.GetProperty("dados").GetProperty("evento").GetString().Should().Be("autenticado");
        _logs.Single().Should().NotContain(CpfValido);
    }
}
