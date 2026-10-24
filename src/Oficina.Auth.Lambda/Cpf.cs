namespace Oficina.Auth.Lambda;

/// <summary>
/// Normalizacao e validacao de CPF (digitos verificadores).
/// Mesma regra do Value Object Documento da API principal, para que um CPF
/// aceito no cadastro de clientes tambem seja aceito na autenticacao.
/// </summary>
public static class Cpf
{
    /// <summary>Remove mascara e qualquer caractere que nao seja digito.</summary>
    public static string Normalizar(string? entrada)
        => new((entrada ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool EhValido(string? entrada)
    {
        var cpf = Normalizar(entrada);
        if (cpf.Length != 11) return false;
        if (cpf.Distinct().Count() == 1) return false; // 000.000.000-00, 111.111.111-11...

        var d1 = Digito(cpf[..9]);
        var d2 = Digito(cpf[..9] + d1);
        return cpf[9] - '0' == d1 && cpf[10] - '0' == d2;
    }

    /// <summary>Mascara para logs: mantem apenas os 2 ultimos digitos (LGPD).</summary>
    public static string Mascarar(string? entrada)
    {
        var cpf = Normalizar(entrada);
        return cpf.Length < 2 ? "***" : $"***.***.***-{cpf[^2..]}";
    }

    private static int Digito(string parcial)
    {
        int soma = 0, peso = parcial.Length + 1;
        foreach (var c in parcial) soma += (c - '0') * peso--;
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
