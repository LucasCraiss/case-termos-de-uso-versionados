using Microsoft.EntityFrameworkCore;
using TermosUso.Aplicacao;
using TermosUso.Dominio;

namespace TermosUso.Infraestrutura;

public sealed class TermosUsoRepositorio(TermosUsoDbContext db) : ITermosUsoRepositorio
{
    public Task<TermoVersao?> ObterVigenteAsync(CancellationToken ct = default) =>
        db.Versoes.FirstOrDefaultAsync(v => v.EhVigente, ct);

    public Task<TermoVersao?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        db.Versoes.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<Usuario?> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);

    /// <summary>
    /// Usuários sujeitos ao aceite.
    ///
    /// Esta é a ÚNICA definição de elegibilidade consultável no banco, e ela consome os
    /// mesmos arrays de <see cref="ElegibilidadeAceite"/> que o caminho em memória usado
    /// pela barreira do login. A contagem da tela administrativa não pode divergir de quem
    /// é de fato barrado, porque é a mesma regra executando.
    ///
    /// Os `.Contains()` abaixo traduzem para `= ANY(...)` justamente porque os membros são
    /// arrays. Com HashSet/IReadOnlySet a tradução não é confiável e o erro só aparece em
    /// tempo de execução — ver a nota em ElegibilidadeAceite.
    /// </summary>
    private IQueryable<Usuario> Elegiveis() =>
        db.Usuarios.Where(u =>
            !ElegibilidadeAceite.PerfisIsentos.Contains(u.Perfil) &&
            ElegibilidadeAceite.StatusConsiderados.Contains(u.Status));

    public async Task<int> ContarElegiveisAsync(CancellationToken ct = default) =>
        await Elegiveis().CountAsync(ct);

    /// <summary>
    /// Listagem administrativa. Projeção pura: não traz conteudo_html, que é a coluna
    /// pesada e não aparece na tabela da tela.
    /// </summary>
    public async Task<IReadOnlyList<VersaoResumo>> ListarResumoAsync(CancellationToken ct = default)
    {
        var totalElegiveis = await ContarElegiveisAsync(ct);

        var linhas = await db.Versoes
            .OrderByDescending(v => v.PublicadoEm)
            .Select(v => new
            {
                v.Id,
                v.Codigo,
                v.PublicadoEm,
                v.EhVigente,
                Aceitaram = db.Aceites.Count(a =>
                    a.TermoVersaoId == v.Id &&
                    db.Usuarios.Any(u =>
                        u.Id == a.UsuarioId &&
                        !ElegibilidadeAceite.PerfisIsentos.Contains(u.Perfil) &&
                        ElegibilidadeAceite.StatusConsiderados.Contains(u.Status)))
            })
            .ToListAsync(ct);

        return linhas
            .Select(l => new VersaoResumo(
                l.Id,
                l.Codigo,
                l.PublicadoEm,
                l.EhVigente,
                totalElegiveis,
                totalElegiveis - l.Aceitaram))
            .ToList();
    }

    /// <summary>
    /// O código vem da sequence do banco, montado em uma ida só.
    /// </summary>
    public async Task<string> ProximoCodigoAsync(CancellationToken ct = default)
    {
        var conexao = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);

        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT 'TU' || LPAD(nextval('termo_versao_codigo_seq')::text, 3, '0')";

        var resultado = await comando.ExecuteScalarAsync(ct);
        return (string)resultado!;
    }

    /// <summary>
    /// Publica a nova versão e desmarca a anterior em UMA transação.
    ///
    /// Duas vigentes, ou nenhuma, são estados que a aplicação inteira assume impossíveis.
    /// Se o processo morresse entre os dois comandos sem transação, o sistema ficaria num
    /// desses estados sem ninguém perceber até o próximo login falhar.
    ///
    /// O índice único parcial é o cinto de segurança: mesmo que este método seja chamado
    /// errado — passando `anterior` nulo quando havia uma vigente — o banco rejeita.
    /// </summary>
    public async Task PublicarAsync(TermoVersao nova, TermoVersao? anterior, CancellationToken ct = default)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        if (anterior is not null)
        {
            anterior.DeixarDeSerVigente();
            db.Versoes.Update(anterior);

            // A anterior precisa sair de vigente ANTES de a nova entrar, senão o índice
            // único parcial rejeita o par dentro da mesma transação.
            await db.SaveChangesAsync(ct);
        }

        db.Versoes.Add(nova);
        await db.SaveChangesAsync(ct);

        await transacao.CommitAsync(ct);
    }

    public Task<bool> ExisteAceiteAsync(Guid usuarioId, Guid versaoId, CancellationToken ct = default) =>
        db.Aceites.AnyAsync(a => a.UsuarioId == usuarioId && a.TermoVersaoId == versaoId, ct);

    public async Task RegistrarAceiteAsync(TermoAceite aceite, CancellationToken ct = default)
    {
        db.Aceites.Add(aceite);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UsuarioPendente>> ListarPendentesAsync(
        Guid versaoId,
        CancellationToken ct = default)
    {
        // Nome e e-mail, e nada além disso: o mínimo para identificar e contatar a pessoa.
        // Perfil e documento não ajudam nessa tarefa e seriam dado a mais exposto numa tela
        // que não precisa deles.
        return await Elegiveis()
            .Where(u => !db.Aceites.Any(a => a.UsuarioId == u.Id && a.TermoVersaoId == versaoId))
            .OrderBy(u => u.Nome)
            .Select(u => new UsuarioPendente(u.Id, u.Nome, u.Email))
            .ToListAsync(ct);
    }
}
