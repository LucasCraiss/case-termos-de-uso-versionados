using TermosUso.Dominio;

namespace TermosUso.Testes;

public class TermoVersaoTestes
{
    private const string ConteudoValido = "<h1>Termo</h1><p>Texto.</p>";

    [Fact]
    public void Publicar_cria_versao_ja_vigente()
    {
        var versao = TermoVersao.Publicar("TU001", ConteudoValido, Guid.CreateVersion7());

        Assert.True(versao.EhVigente);
        Assert.Equal("TU001", versao.Codigo);
        Assert.Equal(ConteudoValido, versao.ConteudoHtml);
    }

    [Fact]
    public void Publicar_aceita_autor_nulo_para_a_versao_semeada()
    {
        // A versão inicial da migração não foi publicada por ninguém: ela é a origem.
        var versao = TermoVersao.Publicar("TU001", ConteudoValido, publicadoPor: null);

        Assert.Null(versao.PublicadoPor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Publicar_recusa_conteudo_vazio(string conteudo)
    {
        var erro = Assert.Throws<RegraDeNegocioException>(
            () => TermoVersao.Publicar("TU001", conteudo, Guid.CreateVersion7()));

        Assert.Equal("RN01", erro.Regra);
    }

    [Fact]
    public void Publicar_recusa_conteudo_acima_do_teto_de_dominio()
    {
        var gigante = new string('a', TermoVersao.TamanhoMaximoConteudo + 1);

        var erro = Assert.Throws<RegraDeNegocioException>(
            () => TermoVersao.Publicar("TU001", gigante, Guid.CreateVersion7()));

        Assert.Equal("TETO_DOMINIO", erro.Regra);
    }

    [Fact]
    public void Publicar_aceita_conteudo_exatamente_no_teto()
    {
        // Fronteira: o teto é inclusivo. Testar só "folgadamente dentro" e "folgadamente
        // fora" deixaria passar um erro de comparação por um caractere.
        var noLimite = new string('a', TermoVersao.TamanhoMaximoConteudo);

        var versao = TermoVersao.Publicar("TU001", noLimite, Guid.CreateVersion7());

        Assert.Equal(TermoVersao.TamanhoMaximoConteudo, versao.ConteudoHtml.Length);
    }

    [Fact]
    public void DeixarDeSerVigente_desmarca_a_versao()
    {
        var versao = TermoVersao.Publicar("TU001", ConteudoValido, Guid.CreateVersion7());

        versao.DeixarDeSerVigente();

        Assert.False(versao.EhVigente);
    }

    [Fact]
    public void DeixarDeSerVigente_recusa_versao_que_ja_nao_e_vigente()
    {
        var versao = TermoVersao.Publicar("TU001", ConteudoValido, Guid.CreateVersion7());
        versao.DeixarDeSerVigente();

        var erro = Assert.Throws<RegraDeNegocioException>(() => versao.DeixarDeSerVigente());

        Assert.Equal("RN04", erro.Regra);
    }

    [Fact]
    public void Entidade_nao_oferece_nenhuma_forma_de_alterar_o_conteudo()
    {
        // Este teste documenta a decisão, não a garante: a garantia é a trigger no banco.
        // Se alguém adicionar um setter ou um método de edição, o teste quebra e a conversa
        // acontece na revisão, não em produção.
        var metodos = typeof(TermoVersao)
            .GetMethods(System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToArray();

        Assert.Equal(["DeixarDeSerVigente"], metodos);

        var propriedadeConteudo = typeof(TermoVersao).GetProperty(nameof(TermoVersao.ConteudoHtml))!;
        Assert.Null(propriedadeConteudo.SetMethod?.IsPublic == true ? propriedadeConteudo.SetMethod : null);
    }
}
