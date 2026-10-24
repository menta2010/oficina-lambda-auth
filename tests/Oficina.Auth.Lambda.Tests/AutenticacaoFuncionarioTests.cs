using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using static Oficina.Auth.Lambda.Tests.AutenticacaoCpfTests;

namespace Oficina.Auth.Lambda.Tests;

public class AutenticacaoFuncionarioTests
{
    private static readonly Guid LojaCentro = Guid.NewGuid();

    internal sealed class FuncionariosFake : IFuncionarioRepository
    {
        private readonly Dictionary<string, FuncionarioAutenticavel> _porUsuario = new();

        public FuncionariosFake Com(FuncionarioAutenticavel f)
        {
            _porUsuario[f.Username] = f;
            return this;
        }

        public Task<FuncionarioAutenticavel?> BuscarPorUsernameAsync(string username, CancellationToken ct)
            => Task.FromResult(_porUsuario.GetValueOrDefault(username));
    }

    private static AutenticacaoFuncionario Criar() => new(
        new FuncionariosFake()
            .Com(new(Guid.NewGuid(), "admin", Pbkdf2.Gerar("admin123"), "Admin", null))
            .Com(new(Guid.NewGuid(), "atendente.centro", Pbkdf2.Gerar("atendente123"), "Atendente", LojaCentro)),
        new JwtEmissor(Jwt));

    private static ClaimsPrincipal Validar(object corpo)
    {
        var token = JsonDocument.Parse(JsonSerializer.Serialize(corpo)).RootElement.GetProperty("token").GetString();
        return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Jwt.Secret))
        }, out _);
    }

    [Fact]
    public async Task Atendente_RecebeTokenComLoja()
    {
        var r = await Criar().AutenticarAsync("atendente.centro", "atendente123", default);

        r.StatusCode.Should().Be(200);
        var principal = Validar(r.Corpo);
        principal.IsInRole("Atendente").Should().BeTrue();
        principal.FindFirst(JwtEmissor.ClaimLoja)!.Value.Should().Be(LojaCentro.ToString());
    }

    [Fact]
    public async Task Admin_RecebeTokenSemLoja()
    {
        var r = await Criar().AutenticarAsync("admin", "admin123", default);

        r.StatusCode.Should().Be(200);
        var principal = Validar(r.Corpo);
        principal.IsInRole("Admin").Should().BeTrue();
        principal.FindFirst(JwtEmissor.ClaimLoja).Should().BeNull();
    }

    [Theory]
    [InlineData("admin", "senha-errada")]
    [InlineData("nao-existe", "admin123")]
    public async Task CredencialInvalida_Retorna401ComMesmaMensagem(string usuario, string senha)
    {
        var r = await Criar().AutenticarAsync(usuario, senha, default);

        r.StatusCode.Should().Be(401);
        JsonSerializer.Serialize(r.Corpo).Should().Contain("Usuario ou senha invalidos");
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("admin", "")]
    public async Task CamposVazios_Retorna400(string? usuario, string senha)
        => (await Criar().AutenticarAsync(usuario, senha, default)).StatusCode.Should().Be(400);

    [Fact]
    public void Pbkdf2_HashMalformado_NaoLanca()
        => Pbkdf2.Verificar("sem-ponto", "x").Should().BeFalse();

    [Fact]
    public void Pbkdf2_CompativelComHashGeradoPelaApi()
    {
        // Hash no formato "salt.hash" (base64), 100k iteracoes SHA256, como o Pbkdf2PasswordHasher da API
        var hash = Pbkdf2.Gerar("senha");
        Pbkdf2.Verificar(hash, "senha").Should().BeTrue();
        Pbkdf2.Verificar(hash, "Senha").Should().BeFalse();
    }

    [Fact]
    public async Task Function_RoteiaLoginPeloRouteKey()
    {
        var function = new Function(
            new AutenticacaoCpf(new ClientesFake(), new JwtEmissor(Jwt)),
            Criar());

        var resp = await function.FunctionHandler(new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "POST /auth/login",
            Body = "{\"usuario\":\"admin\",\"senha\":\"admin123\"}",
            RequestContext = new() { RequestId = "req-login" }
        }, new TestLambdaContext());

        resp.StatusCode.Should().Be(200);
        JsonDocument.Parse(resp.Body).RootElement.GetProperty("perfil").GetString().Should().Be("Admin");
    }
}
