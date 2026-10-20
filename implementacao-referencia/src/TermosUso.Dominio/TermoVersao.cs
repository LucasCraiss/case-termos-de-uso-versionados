namespace TermosUso.Dominio;

/// <summary>
/// Uma versão publicada do Termo de Uso.
///
/// Duas invariantes governam esta entidade, e nenhuma das duas é garantida aqui:
///
/// 1. O conteúdo nunca muda depois de criado. Garantido por trigger no banco, que rejeita
///    UPDATE na coluna. O motivo é que <see cref="TermoAceite"/> aponta para esta linha:
///    alterar o texto faria um usuário constar como tendo concordado com algo que não leu.
///
/// 2. No máximo uma versão vigente. Garantido por índice único parcial sobre
///    <see cref="EhVigente"/>.
///
/// A ausência de setter público abaixo protege contra o erro distraído. Ela NÃO é a garantia:
/// um UPDATE manual, uma migration mal escrita ou um caminho de código novo passariam por cima
/// dela. Por isso as duas invariantes vivem no schema — ver `schema.sql`.
/// </summary>
public sealed class TermoVersao
{
    /// <summary>
    /// Teto de tamanho do conteúdo JÁ SANITIZADO, em caracteres.
    ///
    /// Não existe por performance: a listagem projeta só os metadados e nunca lê esta coluna.
    /// Existe por irreversibilidade. Com UPDATE bloqueado pela trigger, uma linha gigante
    /// gravada por acidente não tem conserto — a única saída seria publicar outra versão,
    /// forçando reaceite de toda a base para corrigir um erro de colagem.
    ///
    /// O número é folgado de propósito. Um termo de uso real fica na casa das dezenas de
    /// kilobytes; este teto é ordens de grandeza maior. A intenção não é barrar documento
    /// legítimo, é pegar o patológico: a colagem acidental do documento três vezes, o dump
    /// de HTML de uma página inteira.
    ///
    /// Há um segundo teto, maior, na borda da requisição — aplicado ANTES da sanitização,
    /// quando o lixo colado do Word ainda não foi removido. Ver TamanhoMaximoBrutoPadrao
    /// em TermosUso.Aplicacao.
    /// </summary>
    public const int TamanhoMaximoConteudo = 2_000_000;

    public Guid Id { get; private set; }

    /// <summary>
    /// Código legível e sequencial, só para exibição. Gerado por sequence do banco, nunca
    /// por MAX+1 na aplicação: duas publicações concorrentes leriam o mesmo máximo.
    ///
    /// Nada referencia uma versão por este código. <see cref="TermoAceite"/> usa o
    /// <see cref="Id"/>, porque o vínculo entre a assinatura e o documento assinado não pode
    /// depender de um rótulo de apresentação.
    /// </summary>
    public string Codigo { get; private set; } = null!;

    /// <summary>
    /// O documento em si: HTML semântico já sanitizado. Não há arquivo em bucket — esta
    /// coluna é a fonte da verdade da versão.
    /// </summary>
    public string ConteudoHtml { get; private set; } = null!;

    public DateTimeOffset PublicadoEm { get; private set; }

    /// <summary>Nulo na versão semeada na migração inicial: ela não foi publicada por ninguém, ela é a origem.</summary>
    public Guid? PublicadoPor { get; private set; }

    public bool EhVigente { get; private set; }

    private TermoVersao() { }

    /// <summary>
    /// Cria uma nova versão, já vigente. O chamador é responsável por desmarcar a anterior
    /// na MESMA transação — ver <see cref="DeixarDeSerVigente"/>.
    /// </summary>
    /// <param name="codigo">Vindo da sequence do banco, não inventado aqui.</param>
    /// <param name="conteudoHtmlSanitizado">Conteúdo que JÁ passou pelo sanitizador.</param>
    public static TermoVersao Publicar(
        string codigo,
        string conteudoHtmlSanitizado,
        Guid? publicadoPor,
        DateTimeOffset? publicadoEm = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("Código da versão é obrigatório.", nameof(codigo));

        if (string.IsNullOrWhiteSpace(conteudoHtmlSanitizado))
        {
            throw new RegraDeNegocioException(
                "RN01",
                "Nenhuma versão pode existir sem conteúdo.");
        }

        if (conteudoHtmlSanitizado.Length > TamanhoMaximoConteudo)
        {
            throw new RegraDeNegocioException(
                "TETO_DOMINIO",
                $"Conteúdo sanitizado tem {conteudoHtmlSanitizado.Length} caracteres e o limite é " +
                $"{TamanhoMaximoConteudo}. O limite existe porque uma versão publicada não pode ser " +
                "corrigida depois — só substituída, ao custo de reaceite de toda a base.");
        }

        return new TermoVersao
        {
            Id = Guid.CreateVersion7(),
            Codigo = codigo,
            ConteudoHtml = conteudoHtmlSanitizado,
            PublicadoPor = publicadoPor,
            PublicadoEm = publicadoEm ?? DateTimeOffset.UtcNow,
            EhVigente = true
        };
    }

    /// <summary>
    /// Único estado mutável da entidade, e só numa direção: vigente deixa de ser vigente.
    /// Não existe o caminho inverso — "voltar" para uma versão antiga significaria republicá-la
    /// como versão nova, o que é exatamente o comportamento correto.
    /// </summary>
    public void DeixarDeSerVigente()
    {
        if (!EhVigente)
        {
            throw new RegraDeNegocioException(
                "RN04",
                $"A versão {Codigo} já não é a vigente.");
        }

        EhVigente = false;
    }
}
