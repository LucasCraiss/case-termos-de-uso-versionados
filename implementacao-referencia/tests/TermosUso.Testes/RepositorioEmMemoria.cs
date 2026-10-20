using TermosUso.Aplicacao;
using TermosUso.Dominio;

namespace TermosUso.Testes;

/// <summary>
/// Dublê de persistência para exercitar o serviço sem banco.
///
/// Ele reproduz o COMPORTAMENTO da implementação real, não as garantias dela: as invariantes
/// de verdade — imutabilidade do conteúdo, no máximo uma vigente, unicidade do aceite — são
/// do schema e só podem ser verificadas contra um Postgres real. É a lacuna consciente desta
/// suíte, registrada no README.
/// </summary>
public sealed class RepositorioEmMemoria : ITermosUsoRepositorio
{
    private readonly List<TermoVersao> _versoes = [];
    private readonly List<TermoAceite> _aceites = [];
    private readonly List<Usuario> _usuarios = [];
    private int _proximoCodigo = 1;

    public IReadOnlyList<TermoVersao> Versoes => _versoes;
    public IReadOnlyList<TermoAceite> Aceites => _aceites;

    public void Semear(params Usuario[] usuarios) => _usuarios.AddRange(usuarios);

    public Task<TermoVersao?> ObterVigenteAsync(CancellationToken ct = default) =>
        Task.FromResult(_versoes.FirstOrDefault(v => v.EhVigente));

    public Task<TermoVersao?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_versoes.FirstOrDefault(v => v.Id == id));

    public Task<Usuario?> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(u => u.Id == usuarioId));

    public Task<string> ProximoCodigoAsync(CancellationToken ct = default) =>
        Task.FromResult($"TU{_proximoCodigo++:000}");

    public Task<int> ContarElegiveisAsync(CancellationToken ct = default) =>
        Task.FromResult(_usuarios.Count(u => u.EstaSujeitoAoAceite()));

    public Task<IReadOnlyList<VersaoResumo>> ListarResumoAsync(CancellationToken ct = default)
    {
        var elegiveis = _usuarios.Where(u => u.EstaSujeitoAoAceite()).ToList();

        IReadOnlyList<VersaoResumo> resumo = _versoes
            .OrderByDescending(v => v.PublicadoEm)
            .Select(v => new VersaoResumo(
                v.Id,
                v.Codigo,
                v.PublicadoEm,
                v.EhVigente,
                elegiveis.Count,
                elegiveis.Count(u => !_aceites.Any(a => a.UsuarioId == u.Id && a.TermoVersaoId == v.Id))))
            .ToList();

        return Task.FromResult(resumo);
    }

    public Task PublicarAsync(TermoVersao nova, TermoVersao? anterior, CancellationToken ct = default)
    {
        anterior?.DeixarDeSerVigente();
        _versoes.Add(nova);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteAceiteAsync(Guid usuarioId, Guid versaoId, CancellationToken ct = default) =>
        Task.FromResult(_aceites.Any(a => a.UsuarioId == usuarioId && a.TermoVersaoId == versaoId));

    public Task RegistrarAceiteAsync(TermoAceite aceite, CancellationToken ct = default)
    {
        _aceites.Add(aceite);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UsuarioPendente>> ListarPendentesAsync(Guid versaoId, CancellationToken ct = default)
    {
        IReadOnlyList<UsuarioPendente> pendentes = _usuarios
            .Where(u => u.EstaSujeitoAoAceite())
            .Where(u => !_aceites.Any(a => a.UsuarioId == u.Id && a.TermoVersaoId == versaoId))
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioPendente(u.Id, u.Nome, u.Email))
            .ToList();

        return Task.FromResult(pendentes);
    }
}
