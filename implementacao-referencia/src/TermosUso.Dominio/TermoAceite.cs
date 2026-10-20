namespace TermosUso.Dominio;

/// <summary>
/// O aceite de uma versão do termo por um usuário. É uma assinatura.
///
/// Append-only por construção: repare que esta classe não expõe NENHUM método além da fábrica.
/// Não é uma convenção que alguém precisa lembrar — não existe código capaz de alterar um
/// aceite depois de criado, porque não há o que chamar.
///
/// No banco, UNIQUE (usuario_id, termo_versao_id) impede que reenvio de requisição ou duplo
/// clique virem dois registros da mesma assinatura.
/// </summary>
public sealed class TermoAceite
{
    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    /// <summary>
    /// Referência pelo id da versão, nunca pelo código legível. O código é rótulo de
    /// apresentação; o vínculo entre a assinatura e o documento assinado não pode depender dele.
    /// </summary>
    public Guid TermoVersaoId { get; private set; }

    public DateTimeOffset AceitoEm { get; private set; }

    /// <summary>
    /// Nulo quando a origem não é conhecida — notadamente nos registros convertidos da
    /// migração inicial, em que o modelo antigo não guardava essa informação.
    /// </summary>
    public string? IpOrigem { get; private set; }

    private TermoAceite() { }

    public static TermoAceite Registrar(
        Guid usuarioId,
        Guid termoVersaoId,
        DateTimeOffset? aceitoEm = null,
        string? ipOrigem = null)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("Usuário é obrigatório.", nameof(usuarioId));

        if (termoVersaoId == Guid.Empty)
            throw new ArgumentException("Versão do termo é obrigatória.", nameof(termoVersaoId));

        return new TermoAceite
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            TermoVersaoId = termoVersaoId,
            AceitoEm = aceitoEm ?? DateTimeOffset.UtcNow,
            IpOrigem = ipOrigem
        };
    }
}
