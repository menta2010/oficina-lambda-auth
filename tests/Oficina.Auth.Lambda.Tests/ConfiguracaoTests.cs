using FluentAssertions;
using Npgsql;

namespace Oficina.Auth.Lambda.Tests;

public class ConfiguracaoTests
{
    [Fact]
    public async Task Le_banco_e_jwt_do_secrets_manager_quando_nao_ha_variaveis_locais()
    {
        var env = new Dictionary<string, string>
        {
            ["DB_SECRET_ARN"] = "arn:db",
            ["JWT_SECRET_ARN"] = "arn:jwt",
            ["JWT_EXPIRATION_MINUTES"] = "15"
        };
        var segredos = new Dictionary<string, string>
        {
            ["arn:db"] = """{"host":"db.local","port":5432,"dbname":"oficina","username":"app","password":"s3nha"}""",
            ["arn:jwt"] = "chave-de-teste-com-pelo-menos-32-bytes!!\n"
        };

        var (cs, jwt) = await Configuracao.CarregarAsync(env.GetValueOrDefault, arn => Task.FromResult(segredos[arn]));

        var builder = new NpgsqlConnectionStringBuilder(cs);
        builder.Host.Should().Be("db.local");
        builder.Database.Should().Be("oficina");
        builder.SslMode.Should().Be(SslMode.Require);
        jwt!.Secret.Should().Be("chave-de-teste-com-pelo-menos-32-bytes!!");
        jwt.ExpiracaoMinutos.Should().Be(15);
        jwt.Issuer.Should().Be("oficina-api");
    }

    [Fact]
    public async Task Sem_configuracao_falha_com_mensagem_clara()
    {
        var acao = () => Configuracao.CarregarAsync(_ => null, _ => Task.FromResult(""));

        await acao.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DB_SECRET_ARN*");
    }
}
