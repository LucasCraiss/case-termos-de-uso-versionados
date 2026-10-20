using AngleSharp.Html.Parser;
using Ganss.Xss;
using TermosUso.Dominio;

namespace TermosUso.Aplicacao;

/// <summary>
/// Limpa o HTML vindo do editor, por ALLOWLIST, no momento da INGESTÃO.
///
/// Duas decisões embutidas aqui, ambas deliberadas:
///
/// ALLOWLIST, não blocklist. Blocklist é uma aposta de que você pensou em todos os vetores de
/// ataque. Allowlist é uma afirmação sobre o que o documento precisa — e um termo de uso
/// precisa de muito pouco: títulos, parágrafos, ênfase, listas e links.
///
/// NA INGESTÃO, não na renderização. O banco só guarda HTML já limpo, e a trigger de
/// imutabilidade garante que ele continue limpo para sempre. Se a limpeza fosse na exibição,
/// cada lugar que mostra o termo precisaria lembrar de limpar — e uma falha futura no
/// sanitizador exporia todo o histórico retroativamente, não só o conteúdo novo.
///
/// E, principalmente: NÃO escrevemos um sanitizador. Parsing de HTML é cheio de casos
/// patológicos que um parser caseiro erra silenciosamente. Usamos uma biblioteca madura,
/// apertada ao mínimo. O valor desta classe é a configuração, não o algoritmo.
/// </summary>
public sealed class SanitizadorDeTermo
{
    /// <summary>
    /// Teto do conteúdo BRUTO, antes da limpeza. É o teto de borda, maior que o teto de
    /// domínio em <see cref="TermoVersao.TamanhoMaximoConteudo"/>.
    ///
    /// São dois porque a colagem vinda de um editor de texto chega inchada de marcação
    /// descartável. Um teto único teria que ser frouxo o bastante para caber esse inchaço —
    /// e aí não limita o que importa — ou apertado o bastante para barrar o conteúdo limpo
    /// legítimo. Dois tetos deixam cada um fazer o seu trabalho.
    /// </summary>
    public const int TamanhoMaximoBrutoPadrao = 20_000_000;

    private static readonly string[] TagsPermitidas =
    [
        "h1", "h2", "h3",   // o documento real tem três níveis de título — não mais, não menos
        "p", "br",
        "strong",
        "ul", "ol", "li",
        "a"
    ];

    /// <summary>
    /// Elementos cujo CONTEÚDO também tem que sumir, e não só a tag.
    ///
    /// Esta lista existe por causa de uma interação que só apareceu nos testes.
    /// <c>KeepChildNodes</c> (configurado abaixo) preserva o texto de dentro de uma tag
    /// removida — é o que faz colar um parágrafo embrulhado em &lt;div&gt; ou &lt;span&gt;
    /// manter o parágrafo em vez de apagá-lo. Só que ele trata TODA tag removida assim,
    /// inclusive &lt;script&gt;: a tag some, o corpo do script vira texto, e o leitor passa
    /// a ver `roubar()` escrito no meio do termo.
    ///
    /// Não é falha de segurança — nada executa — mas é lixo visível num documento legal, e
    /// com a trigger de imutabilidade esse lixo seria permanente.
    ///
    /// Então os dois comportamentos precisam coexistir: para a maioria das tags, manter o
    /// texto; para estas, descartar a subárvore inteira antes de o sanitizador ver o
    /// documento. É uma passada anterior, não uma exceção dentro da allowlist — a allowlist
    /// continua sendo a única coisa que decide o que PASSA.
    /// </summary>
    private static readonly string[] ElementosDescartadosComConteudo =
        ["script", "style", "noscript", "template", "iframe", "object", "embed"];

    private static readonly HtmlParser Parser = new();

    private readonly HtmlSanitizer _sanitizador;
    private readonly int _tamanhoMaximoBruto;

    public SanitizadorDeTermo(int? tamanhoMaximoBruto = null)
    {
        _tamanhoMaximoBruto = tamanhoMaximoBruto ?? TamanhoMaximoBrutoPadrao;

        _sanitizador = new HtmlSanitizer();

        // Zera tudo o que a biblioteca permite por padrão e reconstrói a partir do nada.
        // Partir do default e remover é blocklist disfarçada de allowlist.
        _sanitizador.AllowedTags.Clear();
        foreach (var tag in TagsPermitidas)
            _sanitizador.AllowedTags.Add(tag);

        // Único atributo aceito em todo o documento: o destino do link.
        // Sem class, sem style, sem id — é o que a colagem do Word traz aos montes, e nada
        // disso tem função num texto legal.
        _sanitizador.AllowedAttributes.Clear();
        _sanitizador.AllowedAttributes.Add("href");

        // Sem javascript:, sem data:. Um link que não leva a uma página não é um link.
        _sanitizador.AllowedSchemes.Clear();
        _sanitizador.AllowedSchemes.Add("https");
        _sanitizador.AllowedSchemes.Add("http");
        _sanitizador.AllowedSchemes.Add("mailto");

        // Nenhum CSS sobrevive. Na prática `style` já não passa pela allowlist de
        // atributos acima, mas zerar aqui também fecha a porta caso alguém adicione
        // `style` sem perceber o que vem junto.
        _sanitizador.AllowedCssProperties.Clear();
        _sanitizador.AllowedAtRules.Clear();

        // Ao remover uma tag não permitida, preserva o texto que estava dentro dela.
        // Sem isto, colar um parágrafo embrulhado em <span> ou <div> apagaria o parágrafo
        // junto com o embrulho — o usuário veria o texto sumir sem explicação.
        _sanitizador.KeepChildNodes = true;
    }

    /// <summary>
    /// Aplica o teto de borda e devolve o HTML limpo.
    /// </summary>
    /// <exception cref="RegraDeNegocioException">Conteúdo bruto acima do teto de borda.</exception>
    public string Sanitizar(string htmlBruto)
    {
        ArgumentNullException.ThrowIfNull(htmlBruto);

        if (htmlBruto.Length > _tamanhoMaximoBruto)
        {
            throw new RegraDeNegocioException(
                "TETO_BORDA",
                $"Conteúdo enviado tem {htmlBruto.Length} caracteres e o limite de entrada é " +
                $"{_tamanhoMaximoBruto}. Este teto é aplicado antes da limpeza.");
        }

        return _sanitizador.Sanitize(DescartarElementosNaoTextuais(htmlBruto));
    }

    /// <summary>
    /// Passada prévia: remove a subárvore inteira dos elementos de
    /// <see cref="ElementosDescartadosComConteudo"/>, para que o corpo deles não sobreviva
    /// como texto depois da limpeza.
    /// </summary>
    private static string DescartarElementosNaoTextuais(string html)
    {
        var documento = Parser.ParseDocument(html);

        foreach (var nome in ElementosDescartadosComConteudo)
        {
            foreach (var elemento in documento.QuerySelectorAll(nome).ToArray())
                elemento.Remove();
        }

        return documento.Body?.InnerHtml ?? string.Empty;
    }
}
