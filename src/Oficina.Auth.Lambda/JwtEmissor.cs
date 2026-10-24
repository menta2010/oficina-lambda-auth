using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Oficina.Auth.Lambda;

public sealed record JwtConfig(string Issuer, string Audience, string Secret, int ExpiracaoMinutos);

/// <summary>
/// Emite os JWT de cliente (CPF) e de funcionario (usuario/senha). Usa o mesmo issuer,
/// audience e chave (HS256) que a API principal valida, entao os tokens daqui sao aceitos
/// pelas rotas protegidas. Claims: sub, name, role e, conforme o caso, cpf ou loja_id.
/// </summary>
public sealed class JwtEmissor
{
    public const string RoleCliente = "Cliente";
    public const string ClaimLoja = "loja_id";

    private readonly JwtConfig _config;
    private readonly Func<DateTime> _agora;

    public JwtEmissor(JwtConfig config, Func<DateTime>? agora = null)
    {
        if (Encoding.UTF8.GetByteCount(config.Secret) < 32)
            throw new ArgumentException("A chave do JWT precisa ter pelo menos 32 bytes (HS256).");
        _config = config;
        _agora = agora ?? (() => DateTime.UtcNow);
    }

    public (string Token, DateTime ExpiraEm) Emitir(ClienteAutenticavel cliente, string cpf)
        => Emitir(cliente.Id, cliente.Nome, RoleCliente, new Claim("cpf", cpf));

    public (string Token, DateTime ExpiraEm) Emitir(FuncionarioAutenticavel funcionario)
        => funcionario.LojaId is { } loja
            ? Emitir(funcionario.Id, funcionario.Username, funcionario.Role, new Claim(ClaimLoja, loja.ToString()))
            : Emitir(funcionario.Id, funcionario.Username, funcionario.Role);

    private (string Token, DateTime ExpiraEm) Emitir(Guid sub, string nome, string role, params Claim[] extras)
    {
        var agora = _agora();
        var expiraEm = agora.AddMinutes(_config.ExpiracaoMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, sub.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, nome),
            new(ClaimTypes.Role, role)
        };
        claims.AddRange(extras);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.Secret));
        var token = new JwtSecurityToken(
            issuer: _config.Issuer,
            audience: _config.Audience,
            claims: claims,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
