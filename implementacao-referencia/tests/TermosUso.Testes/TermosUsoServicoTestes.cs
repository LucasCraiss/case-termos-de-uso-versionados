using TermosUso.Aplicacao;
using TermosUso.Dominio;

namespace TermosUso.Testes;

public class TermosUsoServicoTestes
{
    private readonly RepositorioEmMemoria _repositorio = new();
    private readonly TermosUsoServico _servico;
    private readonly Guid _admin = Guid.CreateVersion7();

    private const string TermoInicial = "<h1>Termo</h1><p>Cláusula primeira.</p>";

    public TermosUsoServicoTestes()
    {
        _servico = new TermosUsoServico(_repositorio, new SanitizadorDeTermo(), TimeProvider.System);
    }

    // --- Publicação ----------------------------------------------------------

    [Fact]
    public async Task Publicar_a_primeira_versao_funciona_sem_vigente_anterior()
    {
        var versao = await _servico.PublicarAsync(TermoInicial, _admin);

        Assert.True(versao.EhVigente);
        Assert.Equal("TU001", versao.Codigo);
    }

    [Fact]
    public async Task Publicar_versao_nova_desmarca_a_anterior()
    {
        var primeira = await _servico.PublicarAsync(TermoInicial, _admin);
        var segunda = await _servico.PublicarAsync("<h1>Termo</h1><p>Cláusula revisada.</p>", _admin);

        Assert.False(primeira.EhVigente);
        Assert.True(segunda.EhVigente);
        Assert.Single(_repositorio.Versoes, v => v.EhVigente);
    }

    [Fact]
    public async Task Publicar_conteudo_igual_ao_vigente_e_recusado()
    {
        await _servico.PublicarAsync(TermoInicial, _admin);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => _servico.PublicarAsync(TermoInicial, _admin));

        Assert.Equal("RN03", erro.Regra);
        Assert.Single(_repositorio.Versoes);
    }

    [Fact]
    public async Task Publicar_conteudo_que_so_difere_na_indentacao_e_recusado()
    {
        await _servico.PublicarAsync(TermoInicial, _admin);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => _servico.PublicarAsync("<h1>Termo</h1>\n  <p>Cláusula primeira.</p>", _admin));

        Assert.Equal("RN03", erro.Regra);
    }

    [Fact]
    public async Task Publicar_conteudo_que_so_tem_sujeira_removivel_e_recusado()
    {
        // A comparação acontece DEPOIS de sanitizar. Colar o mesmo texto vindo do Word,
        // cheio de class e style, não é mudança nenhuma no documento — e publicar assim
        // forçaria reaceite de toda a base sem nenhuma alteração real.
        await _servico.PublicarAsync(TermoInicial, _admin);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => _servico.PublicarAsync(
                "<h1 class=\"MsoTitle\">Termo</h1><p style=\"margin:0\">Cláusula primeira.</p>",
                _admin));

        Assert.Equal("RN03", erro.Regra);
    }

    [Fact]
    public async Task Publicar_conteudo_que_vira_vazio_depois_da_limpeza_e_recusado()
    {
        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => _servico.PublicarAsync("<script>roubar()</script>", _admin));

        Assert.Equal("RN01", erro.Regra);
    }

    [Fact]
    public async Task Contexto_de_edicao_traz_o_vigente_e_a_contagem_de_afetados()
    {
        _repositorio.Semear(
            Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo),
            Usuario.Criar("Bruno", "bruno@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo),
            Usuario.Criar("Diego", "diego@exemplo.test", PerfilUsuario.Suporte, StatusUsuario.Ativo));

        await _servico.PublicarAsync(TermoInicial, _admin);

        var contexto = await _servico.ObterContextoDeEdicaoAsync();

        Assert.Contains("Cláusula primeira", contexto.ConteudoVigente);
        Assert.Equal(2, contexto.UsuariosAfetados); // Diego é suporte: isento
    }

    // --- Login ---------------------------------------------------------------

    [Fact]
    public async Task Usuario_sujeito_sem_aceite_e_barrado_no_login()
    {
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        _repositorio.Semear(ana);
        await _servico.PublicarAsync(TermoInicial, _admin);

        var situacao = await _servico.VerificarNoLoginAsync(ana.Id);

        Assert.True(situacao.PrecisaAceitar);
        Assert.NotNull(situacao.ConteudoHtml);
    }

    [Fact]
    public async Task Usuario_que_ja_aceitou_a_vigente_entra_direto()
    {
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        _repositorio.Semear(ana);
        var versao = await _servico.PublicarAsync(TermoInicial, _admin);
        await _servico.RegistrarAceiteAsync(ana.Id, versao.Id);

        var situacao = await _servico.VerificarNoLoginAsync(ana.Id);

        Assert.False(situacao.PrecisaAceitar);
    }

    [Fact]
    public async Task Perfil_interno_nao_e_cobrado()
    {
        var diego = Usuario.Criar("Diego", "diego@exemplo.test", PerfilUsuario.Suporte, StatusUsuario.Ativo);
        _repositorio.Semear(diego);
        await _servico.PublicarAsync(TermoInicial, _admin);

        var situacao = await _servico.VerificarNoLoginAsync(diego.Id);

        Assert.False(situacao.PrecisaAceitar);
    }

    [Fact]
    public async Task Administrador_do_cliente_E_cobrado()
    {
        var carla = Usuario.Criar(
            "Carla", "carla@exemplo.test", PerfilUsuario.AdministradorCliente, StatusUsuario.Ativo);
        _repositorio.Semear(carla);
        await _servico.PublicarAsync(TermoInicial, _admin);

        var situacao = await _servico.VerificarNoLoginAsync(carla.Id);

        Assert.True(situacao.PrecisaAceitar);
    }

    [Fact]
    public async Task Aceitar_versao_antiga_nao_libera_o_login()
    {
        // Quem não aceitou a de março não precisa aceitá-la: precisa aceitar a vigente.
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        _repositorio.Semear(ana);
        var primeira = await _servico.PublicarAsync(TermoInicial, _admin);
        await _servico.PublicarAsync("<h1>Termo</h1><p>Cláusula revisada.</p>", _admin);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => _servico.RegistrarAceiteAsync(ana.Id, primeira.Id));

        Assert.Equal("RN05", erro.Regra);
    }

    [Fact]
    public async Task Sem_termo_publicado_o_login_nao_trava()
    {
        // A ausência de configuração não pode virar indisponibilidade total do produto.
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        _repositorio.Semear(ana);

        var situacao = await _servico.VerificarNoLoginAsync(ana.Id);

        Assert.False(situacao.PrecisaAceitar);
    }

    // --- Aceite --------------------------------------------------------------

    [Fact]
    public async Task Registrar_o_mesmo_aceite_duas_vezes_e_idempotente()
    {
        // Duplo clique e reenvio de requisição não viram dois registros nem erro na cara
        // do usuário. O UNIQUE no banco é a garantia; isto evita o round-trip que falharia.
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        _repositorio.Semear(ana);
        var versao = await _servico.PublicarAsync(TermoInicial, _admin);

        await _servico.RegistrarAceiteAsync(ana.Id, versao.Id);
        await _servico.RegistrarAceiteAsync(ana.Id, versao.Id);

        Assert.Single(_repositorio.Aceites);
    }

    [Fact]
    public async Task Aceite_de_uma_versao_nao_vale_para_a_proxima()
    {
        // O aceite antigo continua no histórico — append-only — mas não dispensa o novo.
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        _repositorio.Semear(ana);
        var primeira = await _servico.PublicarAsync(TermoInicial, _admin);
        await _servico.RegistrarAceiteAsync(ana.Id, primeira.Id);

        await _servico.PublicarAsync("<h1>Termo</h1><p>Cláusula revisada.</p>", _admin);

        var situacao = await _servico.VerificarNoLoginAsync(ana.Id);

        Assert.True(situacao.PrecisaAceitar);
        Assert.Single(_repositorio.Aceites); // o aceite da versão antiga não foi apagado
    }

    // --- Listagem ------------------------------------------------------------

    [Fact]
    public async Task Lista_de_pendentes_so_e_oferecida_para_a_versao_vigente()
    {
        var primeira = await _servico.PublicarAsync(TermoInicial, _admin);
        await _servico.PublicarAsync("<h1>Termo</h1><p>Cláusula revisada.</p>", _admin);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => _servico.ListarPendentesAsync(primeira.Id));

        Assert.Equal("RN07", erro.Regra);
    }

    [Fact]
    public async Task Contagem_de_pendentes_usa_a_mesma_regra_da_barreira_do_login()
    {
        // O cenário que justifica a constante compartilhada. Ele não testa uma tela:
        // testa a consistência entre dois caminhos de código.
        var ana = Usuario.Criar("Ana", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo);
        var carla = Usuario.Criar("Carla", "carla@exemplo.test", PerfilUsuario.AdministradorCliente, StatusUsuario.Ativo);
        var diego = Usuario.Criar("Diego", "diego@exemplo.test", PerfilUsuario.Suporte, StatusUsuario.Ativo);
        var elis = Usuario.Criar("Elis", "elis@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Inativo);
        _repositorio.Semear(ana, carla, diego, elis);

        var versao = await _servico.PublicarAsync(TermoInicial, _admin);
        await _servico.RegistrarAceiteAsync(ana.Id, versao.Id);

        var resumo = (await _servico.ListarAsync()).Single();
        var pendentes = await _servico.ListarPendentesAsync(versao.Id);

        // Ana e Carla são elegíveis; Diego é isento; Elis está inativa.
        Assert.Equal(2, resumo.TotalElegiveis);
        Assert.Equal(1, resumo.TotalPendentes);

        // A lista bate com a contagem.
        Assert.Equal(resumo.TotalPendentes, pendentes.Count);

        // E bate com quem é de fato barrado no login.
        foreach (var pendente in pendentes)
        {
            var situacao = await _servico.VerificarNoLoginAsync(pendente.Id);
            Assert.True(situacao.PrecisaAceitar);
        }

        Assert.False((await _servico.VerificarNoLoginAsync(diego.Id)).PrecisaAceitar);
    }
}
