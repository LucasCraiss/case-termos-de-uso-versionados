using System.Text.RegularExpressions;

namespace TermosUso.Aplicacao;

/// <summary>
/// Decide se o texto do editor MUDOU em relação à versão vigente.
///
/// Esta classe é a fricção deliberada da RN03, e vale explicar por que ela existe.
///
/// No fluxo anterior, publicar uma versão exigia escolher um arquivo HTML no disco — um ato
/// deliberado, que ninguém faz por distração. Trocar o upload por um editor rico pré-preenchido
/// com a versão vigente foi uma melhoria óbvia de usabilidade, e removeu essa barreira sem
/// avisar: o caminho para publicar virou abrir → digitar → salvar.
///
/// E o custo do erro é alto. Publicar sem querer força reaceite de TODA a base, e não existe
/// desfazer: a única correção seria publicar outra versão, forçando um segundo reaceite para
/// consertar o primeiro.
///
/// Então a barreira foi recriada de propósito, no ponto certo. Repare no que é comparado:
/// o CONTEÚDO, não o fato de ter havido digitação. É isso que faz "editar e depois desfazer
/// até o texto original" desabilitar o botão de novo — a diferença entre esta implementação
/// e a ingênua, que marcaria o formulário como sujo ao primeiro caractere.
///
/// Toda melhoria de usabilidade mexe no custo do erro. Facilitar o caminho certo facilita
/// o caminho errado junto.
/// </summary>
public static class ComparadorDeConteudo
{
    private static readonly Regex EspacosEntreTags = new(@">\s+<", RegexOptions.Compiled);
    private static readonly Regex EspacosRepetidos = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Compara os dois conteúdos JÁ SANITIZADOS, ignorando diferenças que não mudam o
    /// documento: indentação, quebras de linha entre tags, espaços repetidos.
    ///
    /// Sem essa normalização, o editor reserializando o mesmo texto com indentação diferente
    /// marcaria uma mudança inexistente — e o botão habilitaria sem ninguém ter alterado nada,
    /// que é precisamente o que a regra quer impedir.
    /// </summary>
    public static bool HouveMudanca(string conteudoVigente, string conteudoNovo)
    {
        ArgumentNullException.ThrowIfNull(conteudoVigente);
        ArgumentNullException.ThrowIfNull(conteudoNovo);

        return !string.Equals(
            Normalizar(conteudoVigente),
            Normalizar(conteudoNovo),
            StringComparison.Ordinal);
    }

    private static string Normalizar(string html)
    {
        var semEspacoEntreTags = EspacosEntreTags.Replace(html, "><");
        return EspacosRepetidos.Replace(semEspacoEntreTags, " ").Trim();
    }
}
