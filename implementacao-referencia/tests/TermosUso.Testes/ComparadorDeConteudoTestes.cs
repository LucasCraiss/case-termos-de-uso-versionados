using TermosUso.Aplicacao;

namespace TermosUso.Testes;

/// <summary>
/// Estes testes cobrem a regra que separa "o botão habilita quando alguém digita" de
/// "o botão habilita quando o conteúdo mudou". A primeira implementação é a óbvia e passa
/// no caso trivial; só o caso do desfazer revela a diferença.
/// </summary>
public class ComparadorDeConteudoTestes
{
    private const string Vigente = "<h1>Termo</h1><p>Cláusula primeira.</p>";

    [Fact]
    public void Conteudo_identico_nao_e_mudanca()
    {
        Assert.False(ComparadorDeConteudo.HouveMudanca(Vigente, Vigente));
    }

    [Fact]
    public void Conteudo_diferente_e_mudanca()
    {
        const string novo = "<h1>Termo</h1><p>Cláusula primeira, revisada.</p>";

        Assert.True(ComparadorDeConteudo.HouveMudanca(Vigente, novo));
    }

    [Fact]
    public void Editar_e_desfazer_ate_o_original_volta_a_nao_ser_mudanca()
    {
        // O cenário que mais vale do conjunto todo.
        var editado = Vigente.Replace("primeira", "segunda");
        Assert.True(ComparadorDeConteudo.HouveMudanca(Vigente, editado));

        var desfeito = editado.Replace("segunda", "primeira");
        Assert.False(ComparadorDeConteudo.HouveMudanca(Vigente, desfeito));
    }

    [Fact]
    public void Indentacao_e_quebra_de_linha_entre_tags_nao_sao_mudanca()
    {
        // O editor pode reserializar o mesmo documento com espaçamento diferente. Sem
        // normalizar, o botão habilitaria sem ninguém ter alterado nada — exatamente o
        // que a regra existe para impedir.
        const string reindentado = "<h1>Termo</h1>\n  <p>Cláusula primeira.</p>\n";

        Assert.False(ComparadorDeConteudo.HouveMudanca(Vigente, reindentado));
    }

    [Fact]
    public void Espaco_dentro_do_texto_nao_e_mudanca_quando_so_muda_a_quantidade()
    {
        const string comEspacosRepetidos = "<h1>Termo</h1><p>Cláusula    primeira.</p>";

        Assert.False(ComparadorDeConteudo.HouveMudanca(Vigente, comEspacosRepetidos));
    }

    [Fact]
    public void Diferenca_de_caixa_no_texto_e_mudanca()
    {
        // Normalizamos espaço, não conteúdo. "Cláusula" e "CLÁUSULA" são textos diferentes
        // num documento legal, e a comparação é ordinal de propósito.
        const string outraCaixa = "<h1>Termo</h1><p>CLÁUSULA primeira.</p>";

        Assert.True(ComparadorDeConteudo.HouveMudanca(Vigente, outraCaixa));
    }
}
