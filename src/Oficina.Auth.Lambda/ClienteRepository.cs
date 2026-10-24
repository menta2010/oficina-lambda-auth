using Npgsql;

namespace Oficina.Auth.Lambda;

/// <summary>Dados minimos do cliente necessarios para autenticar.</summary>
public sealed record ClienteAutenticavel(Guid Id, string Nome, bool Ativo);

/// <summary>Dados minimos do funcionario necessarios para autenticar.</summary>
public sealed record FuncionarioAutenticavel(Guid Id, string Username, string PasswordHash, string Role, Guid? LojaId);

public interface IClienteRepository
{
    Task<ClienteAutenticavel?> BuscarPorCpfAsync(string cpf, CancellationToken ct);
}

public interface IFuncionarioRepository
{
    Task<FuncionarioAutenticavel?> BuscarPorUsernameAsync(string username, CancellationToken ct);
}

/// <summary>
/// Consultas diretas (Npgsql, sem EF) nas tabelas da API principal. O schema e versionado
/// pelas migrations da aplicacao; aqui apenas leitura, com parametros (sem SQL injection).
/// </summary>
public sealed class PostgresAutenticacaoRepository : IClienteRepository, IFuncionarioRepository
{
    private const string SqlCliente = """
        SELECT "Id", "Nome", "Ativo"
        FROM "Clientes"
        WHERE "Documento" = @cpf
        LIMIT 1
        """;

    private const string SqlFuncionario = """
        SELECT "Id", "Username", "PasswordHash", "Role", "LojaId"
        FROM "Usuarios"
        WHERE "Username" = @username
        LIMIT 1
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresAutenticacaoRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<ClienteAutenticavel?> BuscarPorCpfAsync(string cpf, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(SqlCliente);
        cmd.Parameters.AddWithValue("cpf", cpf);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new ClienteAutenticavel(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2));
    }

    public async Task<FuncionarioAutenticavel?> BuscarPorUsernameAsync(string username, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand(SqlFuncionario);
        cmd.Parameters.AddWithValue("username", username);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new FuncionarioAutenticavel(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4));
    }
}
