using Microsoft.EntityFrameworkCore;
using Npgsql;
using TermosUso.Aplicacao;
using TermosUso.Dominio;
using TermosUso.Infraestrutura;

namespace TermosUso.Testes;

/// <summary>
/// Os testes que realmente importam neste projeto.
///
/// Os outros arquivos verificam o comportamento do código C#. Estes verificam o que o BANCO
/// garante sozinho — e é só aqui que a tese central da implementação pode ser demonstrada:
/// as invariantes valem mesmo quando a aplicação tenta violá-las.
///
/// Repare que vários destes testes atacam o banco POR FORA do domínio, com SQL direto. É
/// deliberado: se a garantia fosse apenas a ausência de setter na entidade, todos passariam
/// pelo caminho do código e nenhum provaria nada.
///
/// Precisam do Postgres de `docker compose up`. Sem ele, são pulados.
/// </summary>
[Collection(nameof(ColecaoDeBanco))]
public class InvariantesDoBancoTestes(BancoDeTeste banco)
{
    private const string Conteudo = "<h1>Termo</h1><p>Cláusula primeira.</p>";

    // --- Invariante 1: no máximo uma versão vigente --------------------------

    [SkippableFact]
    public async Task Indice_parcial_impede_duas_versoes_vigentes()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();

        db.Versoes.Add(TermoVersao.Publicar("TU001", Conteudo, null));
        await db.SaveChangesAsync();

        // Segunda versão marcada como vigente, sem desmarcar a primeira. É exatamente o
        // bug que um `PublicarAsync` mal escrito produziria.
        db.Versoes.Add(TermoVersao.Publicar("TU002", "<p>outro</p>", null));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Contains("ux_termo_versao_vigente", erro.InnerException!.Message);
    }

    [SkippableFact]
    public async Task Indice_parcial_permite_varias_versoes_NAO_vigentes()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();

        var primeira = TermoVersao.Publicar("TU001", Conteudo, null);
        var segunda = TermoVersao.Publicar("TU002", "<p>segunda</p>", null);
        var terceira = TermoVersao.Publicar("TU003", "<p>terceira</p>", null);
        primeira.DeixarDeSerVigente();
        segunda.DeixarDeSerVigente();

        db.Versoes.AddRange(primeira, segunda, terceira);
        await db.SaveChangesAsync();

        // O índice é PARCIAL: ele só restringe as linhas com eh_vigente = true. Se fosse um
        // índice único comum sobre a coluna, duas versões antigas já colidiriam entre si e
        // o histórico seria impossível.
        Assert.Equal(3, await db.Versoes.CountAsync());
    }

    // --- Invariante 2: conteúdo imutável -------------------------------------

    [SkippableFact]
    public async Task Trigger_rejeita_UPDATE_no_conteudo_mesmo_vindo_de_SQL_direto()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();
        var versao = TermoVersao.Publicar("TU001", Conteudo, null);
        db.Versoes.Add(versao);
        await db.SaveChangesAsync();

        // Por fora do domínio, do EF e de qualquer regra escrita em C#.
        var erro = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync(
                "UPDATE termo_versao SET conteudo_html = '<p>texto trocado</p>'"));

        Assert.Contains("imutável", erro.MessageText);

        // E o conteúdo continua lá, intacto.
        var depois = await db.Versoes.AsNoTracking().SingleAsync();
        Assert.Equal(Conteudo, depois.ConteudoHtml);
    }

    [SkippableFact]
    public async Task Trigger_permite_UPDATE_em_eh_vigente()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        // Se a trigger travasse a linha inteira em vez de travar colunas específicas, a
        // própria publicação de uma nova versão seria impossível. Este teste existe para
        // que um futuro "endurecimento" da trigger não quebre o fluxo principal em silêncio.
        await using var db = await banco.NovoContextoLimpoAsync();
        db.Versoes.Add(TermoVersao.Publicar("TU001", Conteudo, null));
        await db.SaveChangesAsync();

        var afetadas = await db.Database.ExecuteSqlRawAsync(
            "UPDATE termo_versao SET eh_vigente = false");

        Assert.Equal(1, afetadas);
    }

    [SkippableFact]
    public async Task Trigger_rejeita_UPDATE_no_codigo()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();
        db.Versoes.Add(TermoVersao.Publicar("TU001", Conteudo, null));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE termo_versao SET codigo = 'TU999'"));
    }

    // --- Invariante 3: a trilha de assinaturas -------------------------------

    [SkippableFact]
    public async Task Unique_impede_registrar_o_mesmo_aceite_duas_vezes()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();
        var (usuario, versao) = await SemearUsuarioEVersaoAsync(db);

        db.Aceites.Add(TermoAceite.Registrar(usuario.Id, versao.Id));
        await db.SaveChangesAsync();

        db.Aceites.Add(TermoAceite.Registrar(usuario.Id, versao.Id));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Contains("ux_termo_aceite_usuario_versao", erro.InnerException!.Message);
    }

    [SkippableFact]
    public async Task Aceite_e_append_only_no_banco()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();
        var (usuario, versao) = await SemearUsuarioEVersaoAsync(db);
        db.Aceites.Add(TermoAceite.Registrar(usuario.Id, versao.Id));
        await db.SaveChangesAsync();

        var erroUpdate = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE termo_aceite SET aceito_em = now()"));
        Assert.Contains("append-only", erroUpdate.MessageText);

        var erroDelete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM termo_aceite"));
        Assert.Contains("append-only", erroDelete.MessageText);
    }

    [SkippableFact]
    public async Task FK_impede_apagar_uma_versao_que_alguem_ja_assinou()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();
        var (usuario, versao) = await SemearUsuarioEVersaoAsync(db);
        db.Aceites.Add(TermoAceite.Registrar(usuario.Id, versao.Id));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM termo_versao"));
    }

    // --- Sequence ------------------------------------------------------------

    [SkippableFact]
    public async Task Sequence_nao_repete_codigo_sob_concorrencia()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();

        // Vinte requisições simultâneas pedindo o próximo código. Com MAX+1 calculado na
        // aplicação, várias leriam o mesmo máximo e devolveriam o mesmo valor.
        var tarefas = Enumerable.Range(0, 20).Select(async _ =>
        {
            var opcoes = new DbContextOptionsBuilder<TermosUsoDbContext>()
                .UseNpgsql(banco.StringDeConexao).Options;
            await using var contexto = new TermosUsoDbContext(opcoes);
            return await new TermosUsoRepositorio(contexto).ProximoCodigoAsync();
        });

        var codigos = await Task.WhenAll(tarefas);

        Assert.Equal(20, codigos.Distinct().Count());
    }

    // --- A consulta de elegibilidade -----------------------------------------

    [SkippableFact]
    public async Task Consulta_de_elegiveis_traduz_para_SQL_e_bate_com_a_regra_em_memoria()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        // Este é o teste que pega a troca de array por HashSet em ElegibilidadeAceite.
        // A tradução de `.Contains()` falha em tempo de execução, então nenhum teste em
        // memória acusaria — só uma consulta de verdade contra um banco de verdade.
        await using var db = await banco.NovoContextoLimpoAsync();

        var todos = new[]
        {
            Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo),
            Usuario.Criar("Bruno", "bruno@exemplo.test", PerfilUsuario.Operador, StatusUsuario.PrimeiroAcesso),
            Usuario.Criar("Carla", "carla@exemplo.test", PerfilUsuario.AdministradorCliente, StatusUsuario.Ativo),
            Usuario.Criar("Diego", "diego@exemplo.test", PerfilUsuario.Suporte, StatusUsuario.Ativo),
            Usuario.Criar("Erica", "erica@exemplo.test", PerfilUsuario.Administrador, StatusUsuario.Ativo),
            Usuario.Criar("Elis", "elis@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Inativo)
        };
        db.Usuarios.AddRange(todos);
        await db.SaveChangesAsync();

        var contagemNoBanco = await new TermosUsoRepositorio(db).ContarElegiveisAsync();
        var contagemEmMemoria = todos.Count(u => u.EstaSujeitoAoAceite());

        Assert.Equal(contagemEmMemoria, contagemNoBanco);
        Assert.Equal(3, contagemNoBanco); // Ana, Bruno e Carla
    }

    // --- Fluxo completo ------------------------------------------------------

    [SkippableFact]
    public async Task Publicar_troca_a_vigente_em_uma_transacao_contra_o_banco_real()
    {
        Skip.IfNot(banco.Disponivel, "Postgres indisponível — rode `docker compose up -d`.");

        await using var db = await banco.NovoContextoLimpoAsync();
        var servico = new TermosUsoServico(
            new TermosUsoRepositorio(db), new SanitizadorDeTermo(), TimeProvider.System);

        await servico.PublicarAsync(Conteudo, Guid.CreateVersion7());
        await servico.PublicarAsync("<h1>Termo</h1><p>Cláusula revisada.</p>", Guid.CreateVersion7());

        var vigentes = await db.Versoes.AsNoTracking().CountAsync(v => v.EhVigente);
        var total = await db.Versoes.AsNoTracking().CountAsync();

        Assert.Equal(1, vigentes);
        Assert.Equal(2, total);
    }

    private static async Task<(Usuario, TermoVersao)> SemearUsuarioEVersaoAsync(TermosUsoDbContext db)
    {
        var usuario = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        var versao = TermoVersao.Publicar("TU001", Conteudo, null);

        db.Usuarios.Add(usuario);
        db.Versoes.Add(versao);
        await db.SaveChangesAsync();

        return (usuario, versao);
    }
}
