using System.Security.Cryptography;

namespace Oficina.Auth.Lambda;

/// <summary>
/// Caso de uso: login de funcionario (admin ou atendente) com usuario e senha.
/// A senha e conferida contra o hash PBKDF2 gravado pela API principal.
/// </summary>
public sealed class AutenticacaoFuncionario
{
    private readonly IFuncionarioRepository _funcionarios;
    private readonly JwtEmissor _jwt;

    public AutenticacaoFuncionario(IFuncionarioRepository funcionarios, JwtEmissor jwt)
    {
        _funcionarios = funcionarios;
        _jwt = jwt;
    }

    public async Task<ResultadoAutenticacao> AutenticarAsync(string? usuario, string? senha, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrEmpty(senha))
            return new(400, new { erro = "Informe usuario e senha." }, "login_incompleto");

        var funcionario = await _funcionarios.BuscarPorUsernameAsync(usuario.Trim(), ct);

        // Mesma resposta para usuario inexistente e senha errada (nao revela quais usuarios existem)
        if (funcionario is null || !Pbkdf2.Verificar(funcionario.PasswordHash, senha))
            return new(401, new { erro = "Usuario ou senha invalidos." }, "login_recusado");

        var (token, expiraEm) = _jwt.Emitir(funcionario);
        return new(200, new
        {
            token,
            tipo = "Bearer",
            expiraEm,
            usuario = funcionario.Username,
            perfil = funcionario.Role,
            lojaId = funcionario.LojaId
        }, "login_funcionario");
    }
}

/// <summary>Verificacao PBKDF2 compativel com o Pbkdf2PasswordHasher da API ("salt.hash" em base64).</summary>
public static class Pbkdf2
{
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static bool Verificar(string hashArmazenado, string senha)
    {
        var partes = hashArmazenado.Split('.', 2);
        if (partes.Length != 2) return false;

        try
        {
            var salt = Convert.FromBase64String(partes[0]);
            var esperado = Convert.FromBase64String(partes[1]);
            var atual = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(esperado, atual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Gera um hash no mesmo formato (usado nos testes).</summary>
    public static string Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
