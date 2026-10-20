using TermosUso.Dominio;

namespace TermosUso.Aplicacao;

/// <summary>
/// Casos de uso do Termo de Uso: publicar versão, consultar histórico, verificar o aceite
/// no login e registrá-lo.
/// </summary>
public sealed class TermosUsoServico(
    ITermosUsoRepositorio repositorio,
    SanitizadorDeTermo sanitizador,
    TimeProvider relogio)
{
    /// <summary>
    /// Publica uma nova versão.
    ///
    /// A ordem dos passos importa e cada um tem um porquê:
    ///   1. Sanitiza — teto de borda e allowlist, antes de qualquer decisão sobre o conteúdo.
    ///   2. Compara com a vigente — RN03. É aqui que a publicação acidental é barrada.
    ///   3. Pede o código à sequence do banco — nunca MAX+1.
    ///   4. Cria a entidade — teto de domínio, sobre o conteúdo já limpo.
    ///   5. Grava nova e anterior na mesma transação — RN04.
    ///
    /// Comparar DEPOIS de sanitizar não é detalhe: o conteúdo guardado é o sanitizado, então
    /// comparar o bruto acusaria mudança sempre que o editor mexesse em algo que a limpeza
    /// vai descartar de qualquer jeito.
    /// </summary>
    public async Task<TermoVersao> PublicarAsync(
        string conteudoHtmlBruto,
        Guid publicadoPor,
        CancellationToken ct = default)
    {
        var conteudoLimpo = sanitizador.Sanitizar(conteudoHtmlBruto);

        if (string.IsNullOrWhiteSpace(conteudoLimpo))
        {
            throw new RegraDeNegocioException(
                "RN01",
                "O conteúdo ficou vazio depois da limpeza — nada do que foi enviado é " +
                "formatação permitida em um termo de uso.");
        }

        var vigente = await repositorio.ObterVigenteAsync(ct);

        if (vigente is not null && !ComparadorDeConteudo.HouveMudanca(vigente.ConteudoHtml, conteudoLimpo))
        {
            throw new RegraDeNegocioException(
                "RN03",
                "O conteúdo é igual ao da versão vigente. Publicar assim criaria uma versão " +
                "idêntica e forçaria o reaceite de toda a base sem nenhuma mudança no documento.");
        }

        var codigo = await repositorio.ProximoCodigoAsync(ct);
        var nova = TermoVersao.Publicar(codigo, conteudoLimpo, publicadoPor, relogio.GetUtcNow());

        await repositorio.PublicarAsync(nova, vigente, ct);

        return nova;
    }

    /// <summary>
    /// O que a interface precisa para abrir o editor: o texto atual, para pré-preencher, e
    /// quantos usuários serão afetados, para a confirmação poder dizer o tamanho do estrago
    /// em vez de um "tem certeza?" genérico.
    /// </summary>
    public async Task<ContextoDeEdicao> ObterContextoDeEdicaoAsync(CancellationToken ct = default)
    {
        var vigente = await repositorio.ObterVigenteAsync(ct);
        var afetados = await repositorio.ContarElegiveisAsync(ct);

        return new ContextoDeEdicao(vigente?.ConteudoHtml ?? string.Empty, afetados);
    }

    public Task<IReadOnlyList<VersaoResumo>> ListarAsync(CancellationToken ct = default) =>
        repositorio.ListarResumoAsync(ct);

    public async Task<ConteudoVersao?> ObterConteudoAsync(Guid versaoId, CancellationToken ct = default)
    {
        var versao = await repositorio.ObterPorIdAsync(versaoId, ct);
        return versao is null ? null : new ConteudoVersao(versao.Id, versao.Codigo, versao.ConteudoHtml);
    }

    /// <summary>
    /// A lista de pendentes só faz sentido para a versão VIGENTE. Pendência em versão antiga
    /// não é acionável: quem não aceitou a de março não precisa aceitá-la, precisa aceitar a
    /// vigente. Oferecer a lista ali seria oferecer uma ação que não leva a lugar nenhum.
    /// </summary>
    public async Task<IReadOnlyList<UsuarioPendente>> ListarPendentesAsync(
        Guid versaoId,
        CancellationToken ct = default)
    {
        var versao = await repositorio.ObterPorIdAsync(versaoId, ct)
            ?? throw new RegraDeNegocioException("NAO_ENCONTRADA", "Versão não encontrada.");

        if (!versao.EhVigente)
        {
            throw new RegraDeNegocioException(
                "RN07",
                "Só a versão vigente expõe a lista de pendentes.");
        }

        return await repositorio.ListarPendentesAsync(versaoId, ct);
    }

    /// <summary>
    /// Chamado no login, DEPOIS da validação de credenciais. Quem não é quem diz ser nunca
    /// chega a esta pergunta.
    ///
    /// Não interrompe sessão já aberta: este caminho só roda no login. Publicar um documento
    /// legal é um ato administrativo; derrubar um usuário do meio de uma tarefa é um incidente.
    /// </summary>
    public async Task<SituacaoDeAceite> VerificarNoLoginAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await repositorio.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new RegraDeNegocioException("NAO_ENCONTRADO", "Usuário não encontrado.");

        if (!usuario.EstaSujeitoAoAceite())
            return new SituacaoDeAceite(false, null, null);

        var vigente = await repositorio.ObterVigenteAsync(ct);

        // Sem termo publicado não há o que exigir. Barrar o login aqui transformaria a
        // ausência de configuração numa indisponibilidade total do produto.
        if (vigente is null)
            return new SituacaoDeAceite(false, null, null);

        if (await repositorio.ExisteAceiteAsync(usuarioId, vigente.Id, ct))
            return new SituacaoDeAceite(false, null, null);

        return new SituacaoDeAceite(true, vigente.Id, vigente.ConteudoHtml);
    }

    /// <summary>
    /// Registra o aceite. Idempotente de propósito: duplo clique ou reenvio de requisição
    /// não viram dois registros nem um erro na cara do usuário. O UNIQUE no banco é a
    /// garantia de verdade; esta checagem só evita o round-trip que iria falhar.
    /// </summary>
    public async Task RegistrarAceiteAsync(
        Guid usuarioId,
        Guid versaoId,
        string? ipOrigem = null,
        CancellationToken ct = default)
    {
        var vigente = await repositorio.ObterVigenteAsync(ct)
            ?? throw new RegraDeNegocioException("SEM_VERSAO", "Não há versão vigente do termo.");

        // Aceitar uma versão que não é a vigente não liberaria o login de qualquer forma,
        // e deixaria no histórico uma assinatura que nunca foi exigida.
        if (versaoId != vigente.Id)
        {
            throw new RegraDeNegocioException(
                "RN05",
                "Só a versão vigente pode ser aceita.");
        }

        if (await repositorio.ExisteAceiteAsync(usuarioId, versaoId, ct))
            return;

        var aceite = TermoAceite.Registrar(usuarioId, versaoId, relogio.GetUtcNow(), ipOrigem);
        await repositorio.RegistrarAceiteAsync(aceite, ct);
    }
}
