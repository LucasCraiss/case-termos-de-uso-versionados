using TermosUso.Dominio;

namespace TermosUso.Aplicacao;

/// <summary>
/// Linha da listagem administrativa. Repare que NÃO traz o conteúdo: a listagem é projeção
/// pura de metadados. Trazer o HTML de todas as versões para desenhar uma tabela seria
/// carregar megabytes para exibir datas.
/// </summary>
public sealed record VersaoResumo(
    Guid Id,
    string Codigo,
    DateTimeOffset PublicadoEm,
    bool EhVigente,
    int TotalElegiveis,
    int TotalPendentes);

public sealed record ConteudoVersao(Guid Id, string Codigo, string ConteudoHtml);

public sealed record UsuarioPendente(Guid Id, string Nome, string Email);

/// <summary>
/// O que a interface precisa saber ao abrir o editor: o conteúdo atual, para pré-preencher,
/// e quantos usuários serão afetados, para a confirmação dizer o tamanho do estrago.
/// </summary>
public sealed record ContextoDeEdicao(string ConteudoVigente, int UsuariosAfetados);

/// <summary>Resultado da verificação feita no login.</summary>
public sealed record SituacaoDeAceite(bool PrecisaAceitar, Guid? VersaoExigidaId, string? ConteudoHtml);
