using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;

namespace Oficina.Auth.Lambda;

/// <summary>
/// Le a configuracao das variaveis de ambiente e, em nuvem, do AWS Secrets Manager.
///
/// Banco: DB_CONNECTION_STRING (local) ou DB_SECRET_ARN, um segredo JSON com
///        host, port, dbname, username e password.
/// JWT:   JWT_SECRET (local) ou JWT_SECRET_ARN (segredo em texto puro),
///        mais JWT_ISSUER, JWT_AUDIENCE e JWT_EXPIRATION_MINUTES.
/// </summary>
public static class Configuracao
{
    public static async Task<(string ConnectionString, JwtConfig? Jwt)> CarregarAsync(
        Func<string, string?>? env = null,
        Func<string, Task<string>>? lerSegredo = null,
        bool exigirJwt = true)
    {
        env ??= Environment.GetEnvironmentVariable;
        lerSegredo ??= LerSegredoAwsAsync;

        var connectionString = env("DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var arn = Obrigatoria(env, "DB_SECRET_ARN");
            connectionString = MontarConnectionString(await lerSegredo(arn));
        }

        if (!exigirJwt) return (connectionString, null); // job de inativacao so usa o banco

        var jwtSecret = env("JWT_SECRET");
        if (string.IsNullOrWhiteSpace(jwtSecret))
            jwtSecret = (await lerSegredo(Obrigatoria(env, "JWT_SECRET_ARN"))).Trim();

        var jwt = new JwtConfig(
            Issuer: env("JWT_ISSUER") ?? "oficina-api",
            Audience: env("JWT_AUDIENCE") ?? "oficina-clients",
            Secret: jwtSecret,
            ExpiracaoMinutos: int.TryParse(env("JWT_EXPIRATION_MINUTES"), out var m) ? m : 60);

        return (connectionString, jwt);
    }

    internal static string MontarConnectionString(string segredoJson)
    {
        using var doc = JsonDocument.Parse(segredoJson);
        var root = doc.RootElement;

        string Ler(string nome) => root.TryGetProperty(nome, out var v)
            ? v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString() ?? string.Empty
            : throw new InvalidOperationException($"Segredo do banco sem o campo '{nome}'.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Ler("host"),
            Port = int.Parse(Ler("port")),
            Database = Ler("dbname"),
            Username = Ler("username"),
            Password = Ler("password"),
            SslMode = SslMode.Require,
            // Lambda: poucas conexoes por instancia, timeouts curtos para falhar rapido
            MaxPoolSize = 2,
            Timeout = 5,
            CommandTimeout = 5
        };
        return builder.ConnectionString;
    }

    private static string Obrigatoria(Func<string, string?> env, string nome)
        => env(nome) is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"Variavel de ambiente {nome} nao configurada.");

    private static async Task<string> LerSegredoAwsAsync(string arn)
    {
        using var client = new AmazonSecretsManagerClient();
        var resp = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = arn });
        return resp.SecretString;
    }
}
