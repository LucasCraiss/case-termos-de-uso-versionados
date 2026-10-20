using TermosUso.Aplicacao;
using TermosUso.Dominio;

namespace TermosUso.Testes;

public class SanitizadorDeTermoTestes
{
    private readonly SanitizadorDeTermo _sanitizador = new();

    [Fact]
    public void Preserva_a_formatacao_permitida()
    {
        const string entrada =
            "<h1>Termo</h1><h2>Seção</h2><h3>Item</h3>" +
            "<p>Texto com <strong>ênfase</strong>.</p>" +
            "<ul><li>um</li></ul><ol><li>dois</li></ol>";

        var saida = _sanitizador.Sanitizar(entrada);

        Assert.Contains("<h1>", saida);
        Assert.Contains("<h3>", saida);
        Assert.Contains("<strong>", saida);
        Assert.Contains("<li>", saida);
    }

    [Fact]
    public void Remove_script()
    {
        var saida = _sanitizador.Sanitizar("<p>ok</p><script>roubar()</script>");

        Assert.DoesNotContain("script", saida, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roubar", saida);
    }

    [Fact]
    public void Remove_manipulador_de_evento_embutido()
    {
        var saida = _sanitizador.Sanitizar("<p onclick=\"roubar()\">texto</p>");

        Assert.DoesNotContain("onclick", saida, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("texto", saida);
    }

    [Fact]
    public void Remove_class_style_e_id_que_a_colagem_do_Word_traz()
    {
        const string coladoDoWord =
            "<p class=\"MsoNormal\" style=\"margin:0cm;font-family:Calibri\" id=\"p1\">Cláusula primeira.</p>";

        var saida = _sanitizador.Sanitizar(coladoDoWord);

        Assert.DoesNotContain("class", saida, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style", saida, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MsoNormal", saida);
        Assert.Contains("Cláusula primeira.", saida);
    }

    [Fact]
    public void Preserva_o_texto_dentro_de_tag_removida()
    {
        // KeepChildNodes. Sem isto, colar um parágrafo embrulhado em <span> ou <div>
        // apagaria o parágrafo junto com o embrulho, e o usuário veria o texto sumir
        // sem explicação nenhuma.
        var saida = _sanitizador.Sanitizar("<div><span>Texto importante</span></div>");

        Assert.Contains("Texto importante", saida);
        Assert.DoesNotContain("<div", saida);
        Assert.DoesNotContain("<span", saida);
    }

    [Fact]
    public void Mantem_link_https_e_descarta_o_resto_do_atributo()
    {
        var saida = _sanitizador.Sanitizar(
            "<a href=\"https://exemplo.test/politica\" target=\"_blank\" rel=\"x\">política</a>");

        Assert.Contains("https://exemplo.test/politica", saida);
        Assert.DoesNotContain("target", saida);
        Assert.DoesNotContain("rel=", saida);
    }

    [Fact]
    public void Remove_link_com_esquema_javascript()
    {
        var saida = _sanitizador.Sanitizar("<a href=\"javascript:roubar()\">clique</a>");

        Assert.DoesNotContain("javascript", saida, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Remove_imagem_mesmo_sem_script()
    {
        // Imagem não está na allowlist porque o documento não tem imagem. Cada recurso a
        // mais é mais superfície de sanitização e mais caso de renderização.
        var saida = _sanitizador.Sanitizar("<p>texto</p><img src=\"https://exemplo.test/x.png\">");

        Assert.DoesNotContain("<img", saida);
    }

    [Fact]
    public void Remove_tabela_preservando_o_texto_das_celulas()
    {
        var saida = _sanitizador.Sanitizar("<table><tr><td>valor</td></tr></table>");

        Assert.DoesNotContain("<table", saida);
        Assert.Contains("valor", saida);
    }

    [Fact]
    public void Recusa_conteudo_acima_do_teto_de_borda()
    {
        var sanitizador = new SanitizadorDeTermo(tamanhoMaximoBruto: 100);

        var erro = Assert.Throws<RegraDeNegocioException>(
            () => sanitizador.Sanitizar(new string('a', 101)));

        Assert.Equal("TETO_BORDA", erro.Regra);
    }

    [Fact]
    public void Teto_de_borda_e_maior_que_o_teto_de_dominio()
    {
        // A relação entre os dois é a razão de existirem dois. Se alguém inverter os
        // números numa configuração, o teto de domínio nunca seria alcançado e a defesa
        // contra a linha patológica deixaria de existir sem nenhum sintoma visível.
        Assert.True(SanitizadorDeTermo.TamanhoMaximoBrutoPadrao > TermoVersao.TamanhoMaximoConteudo);
    }
}
