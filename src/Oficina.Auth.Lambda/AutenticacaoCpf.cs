namespace Oficina.Auth.Lambda;

/// <summary>Resultado do caso de uso, ja no formato HTTP que a Lambda devolve.</summary>
public sealed record ResultadoAutenticacao(int StatusCode, object Corpo, string Evento);

/// <summary>
/// Caso de uso: valida o CPF, consulta existencia e status do cliente e emite o JWT.
/// Nao conhece API Gateway nem Lambda, o que facilita os testes.
/// </summary>
public sealed class AutenticacaoCpf
{
    private readonly IClienteRepository _clientes;
    private readonly JwtEmissor _jwt;

    public AutenticacaoCpf(IClienteRepository clientes, JwtEmissor jwt)
    {
        _clientes = clientes;
        _jwt = jwt;
    }

    public async Task<ResultadoAutenticacao> AutenticarAsync(string? cpfEntrada, CancellationToken ct)
    {
        if (!Cpf.EhValido(cpfEntrada))
            return new(400, new { erro = "CPF invalido." }, "cpf_invalido");

        var cpf = Cpf.Normalizar(cpfEntrada);
        var cliente = await _clientes.BuscarPorCpfAsync(cpf, ct);

        if (cliente is null)
            return new(404, new { erro = "Cliente nao encontrado." }, "cliente_nao_encontrado");

        if (!cliente.Ativo)
            return new(403, new { erro = "Cliente inativo." }, "cliente_inativo");

        var (token, expiraEm) = _jwt.Emitir(cliente, cpf);
        return new(200, new
        {
            token,
            tipo = "Bearer",
            expiraEm,
            cliente = new { id = cliente.Id, nome = cliente.Nome }
        }, "autenticado");
    }
}
