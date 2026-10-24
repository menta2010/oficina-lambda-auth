using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace Oficina.Auth.Lambda.Tests;

public class AutenticacaoCpfTests
{
    internal const string CpfValido = "52998224725";
    internal static readonly JwtConfig Jwt = new("oficina-api", "oficina-clients", "chave-de-teste-com-pelo-menos-32-bytes!!", 60);

    internal sealed class ClientesFake : IClienteRepository
    {
        private readonly Dictionary<string, ClienteAutenticavel> _porCpf = new();
        public int Consultas { get; private set; }

        public ClientesFake Com(string cpf, ClienteAutenticavel cliente)
        {
            _porCpf[cpf] = cliente;
            return this;
        }

        public Task<ClienteAutenticavel?> BuscarPorCpfAsync(string cpf, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(_porCpf.GetValueOrDefault(cpf));
        }
    }

    private static AutenticacaoCpf Criar(ClientesFake clientes) => new(clientes, new JwtEmissor(Jwt));

    [Fact]
    public async Task Cpf_invalido_retorna_400_sem_consultar_o_banco()
    {
        var clientes = new ClientesFake();

        var r = await Criar(clientes).AutenticarAsync("123.456.789-00", default);

        r.StatusCode.Should().Be(400);
        clientes.Consultas.Should().Be(0);
    }

    [Fact]
    public async Task Cliente_inexistente_retorna_404()
    {
        var r = await Criar(new ClientesFake()).AutenticarAsync(CpfValido, default);

        r.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Cliente_inativo_retorna_403()
    {
        var clientes = new ClientesFake().Com(CpfValido, new(Guid.NewGuid(), "Maria", Ativo: false));

        var r = await Criar(clientes).AutenticarAsync(CpfValido, default);

        r.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Cliente_ativo_recebe_jwt_aceito_pela_api_principal()
    {
        var id = Guid.NewGuid();
        var clientes = new ClientesFake().Com(CpfValido, new(id, "Maria", Ativo: true));

        var r = await Criar(clientes).AutenticarAsync("529.982.247-25", default);

        r.StatusCode.Should().Be(200);
        var token = JsonDocument.Parse(JsonSerializer.Serialize(r.Corpo)).RootElement.GetProperty("token").GetString();

        // Mesmos parametros de validacao usados pela API (Oficina.Infrastructure/DependencyInjection.cs)
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Jwt.Secret)),
            ClockSkew = TimeSpan.Zero
        }, out _);

        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(id.ToString());
        principal.IsInRole(JwtEmissor.RoleCliente).Should().BeTrue();
        principal.FindFirst("cpf")!.Value.Should().Be(CpfValido);
    }

    [Fact]
    public void Chave_curta_e_rejeitada()
    {
        var acao = () => new JwtEmissor(Jwt with { Secret = "curta" });

        acao.Should().Throw<ArgumentException>();
    }
}
