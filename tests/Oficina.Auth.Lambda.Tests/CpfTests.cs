using FluentAssertions;

namespace Oficina.Auth.Lambda.Tests;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData(" 529 982 247 25 ")]
    public void Aceita_cpf_valido_com_ou_sem_mascara(string cpf)
        => Cpf.EhValido(cpf).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("529.982.247-24")]   // digito verificador errado
    [InlineData("111.111.111-11")]   // todos os digitos iguais
    [InlineData("1234567890")]       // 10 digitos
    [InlineData("11222333000181")]   // CNPJ nao autentica
    public void Rejeita_cpf_invalido(string? cpf)
        => Cpf.EhValido(cpf).Should().BeFalse();

    [Fact]
    public void Normaliza_removendo_mascara()
        => Cpf.Normalizar("529.982.247-25").Should().Be("52998224725");

    [Fact]
    public void Mascara_mantem_so_os_dois_ultimos_digitos()
        => Cpf.Mascarar("529.982.247-25").Should().Be("***.***.***-25");
}
