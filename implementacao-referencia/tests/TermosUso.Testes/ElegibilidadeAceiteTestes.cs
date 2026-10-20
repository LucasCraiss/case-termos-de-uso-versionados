using TermosUso.Dominio;

namespace TermosUso.Testes;

public class ElegibilidadeAceiteTestes
{
    [Theory]
    [InlineData(PerfilUsuario.SuperAdministrador)]
    [InlineData(PerfilUsuario.Administrador)]
    [InlineData(PerfilUsuario.Suporte)]
    public void Operacao_interna_e_isenta(PerfilUsuario perfil)
    {
        Assert.False(ElegibilidadeAceite.EstaSujeito(perfil, StatusUsuario.Ativo));
    }

    [Fact]
    public void Administrador_do_cliente_NAO_e_isento()
    {
        // O cenário mais importante da suíte.
        //
        // "Administrador" dito sem qualificação pode significar duas coisas muito
        // diferentes, e numa primeira redação da especificação a lista de isentos saiu
        // com dois perfis quando eram três. A interpretação errada isentaria do termo
        // legal exatamente o perfil que mais precisa aceitá-lo — quem responde pela
        // empresa cliente.
        Assert.True(ElegibilidadeAceite.EstaSujeito(
            PerfilUsuario.AdministradorCliente, StatusUsuario.Ativo));
    }

    [Fact]
    public void Operador_e_sujeito_ao_aceite()
    {
        Assert.True(ElegibilidadeAceite.EstaSujeito(PerfilUsuario.Operador, StatusUsuario.Ativo));
    }

    [Theory]
    [InlineData(StatusUsuario.Ativo)]
    [InlineData(StatusUsuario.PrimeiroAcesso)]
    [InlineData(StatusUsuario.RedefinicaoSenha)]
    public void Status_que_terminam_em_sessao_autenticada_contam(StatusUsuario status)
    {
        Assert.True(ElegibilidadeAceite.EstaSujeito(PerfilUsuario.Operador, status));
    }

    [Theory]
    [InlineData(StatusUsuario.Inativo)]
    [InlineData(StatusUsuario.Excluido)]
    public void Quem_nao_faz_login_fica_fora_da_conta(StatusUsuario status)
    {
        // Contá-los inflaria o denominador da tela com gente que nunca vai aceitar, e a
        // contagem de pendentes jamais zeraria.
        Assert.False(ElegibilidadeAceite.EstaSujeito(PerfilUsuario.Operador, status));
    }

    [Fact]
    public void As_listas_sao_arrays_e_nao_conjuntos()
    {
        // Este teste parece pedante e não é. Os membros são consumidos dentro de consultas
        // LINQ-to-SQL, onde `array.Contains(coluna)` traduz e `HashSet.Contains(coluna)`
        // não traduz de forma confiável. O sintoma da troca seria uma exceção em tempo de
        // execução, invisível na compilação e invisível num teste que exercita a constante
        // em memória — ou seja, invisível em todos os outros testes deste arquivo.
        //
        // Se alguém "melhorar" o tipo para HashSet, isto quebra aqui, barato, em vez de
        // quebrar na primeira consulta em produção.
        Assert.IsType<PerfilUsuario[]>(ElegibilidadeAceite.PerfisIsentos);
        Assert.IsType<StatusUsuario[]>(ElegibilidadeAceite.StatusConsiderados);
    }
}
