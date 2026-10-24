using System.Text.Json;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using static Oficina.Auth.Lambda.Tests.AutenticacaoCpfTests;

namespace Oficina.Auth.Lambda.Tests;

/// <summary>
/// Executa as consultas reais da Lambda contra um PostgreSQL 16 com o schema gerado pelas
/// migrations do oficina-app (Contrato/schema-oficina.sql). Garante que autenticacao por CPF,
/// login de funcionario e job de inativacao continuam compativeis com as tabelas da aplicacao.
/// Requer Docker.
/// </summary>
public class IntegracaoBancoTests : IAsyncLifetime
{
    // Hash PBKDF2-SHA256 (100k iteracoes, salt 00..0F) de "senha-do-atendente", calculado fora do .NET
    // (Python hashlib) no formato "salt.hash" gravado pelo Pbkdf2PasswordHasher da API.
    private const string HashAtendente = "AAECAwQFBgcICQoLDA0ODw==.eWp48/lu/qvUsAzi1HNFnm39HK/UHnz24ZeF3ZgQ5QA=";

    private static readonly Guid Loja = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ClienteAtivo = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("oficina")
        .Build();

    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_db.GetConnectionString());

        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Contrato", "schema-oficina.sql"));
        await ExecutarAsync(schema);
        await ExecutarAsync($"""
            INSERT INTO "Lojas" ("Id","Nome","Cidade","Ativa") VALUES ('{Loja}','Centro','Sao Paulo',true);
            INSERT INTO "Clientes" ("Id","Nome","Documento","Ativo","CriadoEm") VALUES
              ('{ClienteAtivo}','Maria Ativa','52998224725',true, now()),
              ('10000000-0000-0000-0000-000000000002','Joao Inativo','11144477735',false, now()),
              ('10000000-0000-0000-0000-000000000003','Antigo sem OS','39053344705',true, now() - interval '30 months'),
              ('10000000-0000-0000-0000-000000000004','Antigo com OS aberta','86288366757',true, now() - interval '30 months');
            INSERT INTO "Veiculos" ("Id","ClienteId","Placa","Marca","Modelo","Ano") VALUES
              ('20000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000004','ABC1234','VW','Gol',2020);
            INSERT INTO "OrdensServico" ("Id","LojaId","ClienteId","VeiculoId","Status","Encerrada","CriadaEm") VALUES
              ('30000000-0000-0000-0000-000000000004','{Loja}','10000000-0000-0000-0000-000000000004',
               '20000000-0000-0000-0000-000000000004','EmExecucao',false, now() - interval '28 months');
            INSERT INTO "Usuarios" ("Id","Username","PasswordHash","Role","LojaId") VALUES
              ('40000000-0000-0000-0000-000000000001','atendente.centro','{HashAtendente}','Atendente','{Loja}');
            """);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _db.DisposeAsync();
    }

    private async Task ExecutarAsync(string sql)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }

    private AutenticacaoCpf Cpf() => new(new PostgresAutenticacaoRepository(_dataSource), new JwtEmissor(Jwt));

    [Fact]
    public async Task Cpf_ClienteAtivo_RecebeToken()
    {
        var r = await Cpf().AutenticarAsync("529.982.247-25", default);

        r.StatusCode.Should().Be(200);
        JsonSerializer.Serialize(r.Corpo).Should().Contain(ClienteAtivo.ToString());
    }

    [Theory]
    [InlineData("111.444.777-35", 403)] // existe, mas inativo
    [InlineData("935.411.347-80", 404)] // CPF valido que nao esta na base
    public async Task Cpf_InativoOuInexistente(string cpf, int esperado)
        => (await Cpf().AutenticarAsync(cpf, default)).StatusCode.Should().Be(esperado);

    [Fact]
    public async Task Login_ConfereHashGravadoNoFormatoDaApi()
    {
        var login = new AutenticacaoFuncionario(new PostgresAutenticacaoRepository(_dataSource), new JwtEmissor(Jwt));

        var ok = await login.AutenticarAsync("atendente.centro", "senha-do-atendente", default);
        var errada = await login.AutenticarAsync("atendente.centro", "outra-senha", default);

        ok.StatusCode.Should().Be(200);
        JsonSerializer.Serialize(ok.Corpo).Should().Contain(Loja.ToString());
        errada.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task JobInativacao_SoInativaQuemNaoTemOsRecenteNemAberta()
    {
        Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", _db.GetConnectionString());
        try
        {
            await new InativacaoClientesFunction().FunctionHandler(new { }, new TestLambdaContext());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", null);
        }

        var ativos = new Dictionary<string, bool>();
        await using var cmd = _dataSource.CreateCommand("""SELECT "Nome", "Ativo" FROM "Clientes" """);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ativos[reader.GetString(0)] = reader.GetBoolean(1);

        ativos["Antigo sem OS"].Should().BeFalse();
        ativos["Antigo com OS aberta"].Should().BeTrue();
        ativos["Maria Ativa"].Should().BeTrue("foi criada agora");
    }
}
