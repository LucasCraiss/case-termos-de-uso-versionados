using TermosUso.Dominio;

namespace TermosUso.Aplicacao;

/// <summary>
/// Porta de persistência. A implementação vive em TermosUso.Infraestrutura.
/// </summary>
public interface ITermosUsoRepositorio
{
    Task<TermoVersao?> ObterVigenteAsync(CancellationToken ct = default);

    Task<TermoVersao?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<VersaoResumo>> ListarResumoAsync(CancellationToken ct = default);

    /// <summary>
    /// Próximo código, vindo da sequence do banco. Não é MAX+1 calculado na aplicação:
    /// duas publicações concorrentes leriam o mesmo máximo e gerariam o mesmo código.
    /// </summary>
    Task<string> ProximoCodigoAsync(CancellationToken ct = default);

    /// <summary>
    /// Grava a nova versão e desmarca a anterior em uma transação só. Nunca duas vigentes,
    /// nunca nenhuma — e o índice único parcial no banco rejeita a primeira hipótese mesmo
    /// que este método seja chamado errado.
    /// </summary>
    Task PublicarAsync(TermoVersao nova, TermoVersao? anterior, CancellationToken ct = default);

    Task<bool> ExisteAceiteAsync(Guid usuarioId, Guid versaoId, CancellationToken ct = default);

    Task RegistrarAceiteAsync(TermoAceite aceite, CancellationToken ct = default);

    /// <summary>Usuários sujeitos ao aceite que ainda não aceitaram a versão informada.</summary>
    Task<IReadOnlyList<UsuarioPendente>> ListarPendentesAsync(Guid versaoId, CancellationToken ct = default);

    Task<int> ContarElegiveisAsync(CancellationToken ct = default);

    Task<Usuario?> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
