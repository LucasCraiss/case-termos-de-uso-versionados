using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using TermosUso.Aplicacao;
using TermosUso.Dominio;
using TermosUso.Infraestrutura;

var builder = WebApplication.CreateBuilder(args);

var stringDeConexao =
    Environment.GetEnvironmentVariable("TERMOS_DB")
    ?? "Host=localhost;Port=5432;Database=termos_uso;Username=postgres;Password=postgres";

builder.Services.AddDbContext<TermosUsoDbContext>(o => o.UseNpgsql(stringDeConexao));
builder.Services.AddScoped<ITermosUsoRepositorio, TermosUsoRepositorio>();
builder.Services.AddScoped<TermosUsoServico>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new SanitizadorDeTermo());

// Teto de borda, camada HTTP: a primeira linha de defesa contra um corpo absurdo é não
// ler o corpo absurdo. O teto em SanitizadorDeTermo é o segundo: vale para quem chama o
// serviço sem passar por HTTP (um job, um teste, um comando de migração).
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = SanitizadorDeTermo.TamanhoMaximoBrutoPadrao);

var app = builder.Build();

await PrepararBancoAsync(app);

// Traduz violação de regra de negócio em 422, e só ela. Erro de argumento é defeito de
// programação e continua virando 500 — são públicos diferentes: um é o usuário, o outro
// é quem mantém o código.
app.Use(async (contexto, proximo) =>
{
    try
    {
        await proximo(contexto);
    }
    catch (RegraDeNegocioException ex)
    {
        contexto.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await contexto.Response.WriteAsJsonAsync(new { regra = ex.Regra, mensagem = ex.Message });
    }
});

// --- Administração das versões -----------------------------------------------

app.MapGet("/termos", async (TermosUsoServico servico, CancellationToken ct) =>
    Results.Ok(await servico.ListarAsync(ct)));

// O que a tela precisa para abrir o editor: o texto vigente, para pré-preencher, e quantos
// usuários serão afetados, para a confirmação dizer o tamanho do estrago.
app.MapGet("/termos/contexto-edicao", async (TermosUsoServico servico, CancellationToken ct) =>
    Results.Ok(await servico.ObterContextoDeEdicaoAsync(ct)));

app.MapGet("/termos/{id:guid}/conteudo", async (
    Guid id, TermosUsoServico servico, CancellationToken ct) =>
{
    var conteudo = await servico.ObterConteudoAsync(id, ct);
    return conteudo is null ? Results.NotFound() : Results.Ok(conteudo);
});

app.MapGet("/termos/{id:guid}/pendentes", async (
    Guid id, TermosUsoServico servico, CancellationToken ct) =>
    Results.Ok(await servico.ListarPendentesAsync(id, ct)));

app.MapPost("/termos", async (
    PublicarRequisicao requisicao, TermosUsoServico servico, CancellationToken ct) =>
{
    var versao = await servico.PublicarAsync(requisicao.ConteudoHtml, requisicao.PublicadoPor, ct);
    return Results.Created($"/termos/{versao.Id}", new { versao.Id, versao.Codigo });
});

// --- Login e aceite ----------------------------------------------------------

// Login simplificado: esta implementação de referência é sobre o termo, não sobre
// autenticação. O ponto a observar é QUANDO a verificação acontece — depois de validar
// credenciais, antes de liberar a sessão.
app.MapPost("/login", async (
    LoginRequisicao requisicao, TermosUsoServico servico, CancellationToken ct) =>
{
    // (aqui entraria a validação de credenciais)
    var situacao = await servico.VerificarNoLoginAsync(requisicao.UsuarioId, ct);

    return situacao.PrecisaAceitar
        ? Results.Ok(new { sessaoLiberada = false, aceitePendente = situacao })
        : Results.Ok(new { sessaoLiberada = true });
});

app.MapPost("/termos/{id:guid}/aceite", async (
    Guid id,
    AceiteRequisicao requisicao,
    TermosUsoServico servico,
    HttpContext contexto,
    CancellationToken ct) =>
{
    await servico.RegistrarAceiteAsync(
        requisicao.UsuarioId,
        id,
        contexto.Connection.RemoteIpAddress?.ToString(),
        ct);

    return Results.NoContent();
});

app.Run();


static async Task PrepararBancoAsync(WebApplication app)
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<TermosUsoDbContext>();

    // Numa aplicação real isto seria uma migration versionada. Aqui o schema é aplicado
    // direto para que `docker compose up` e `dotnet run` bastem para ver tudo funcionando.
    var caminho = Path.Combine(AppContext.BaseDirectory, "schema.sql");
    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(caminho));

    if (!await db.Usuarios.AnyAsync())
        await SemearAsync(db);
}

static async Task SemearAsync(TermosUsoDbContext db)
{
    db.Usuarios.AddRange(
        Usuario.Criar("Ana Operadora", "ana@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Ativo),
        Usuario.Criar("Bruno Operador", "bruno@exemplo.test", PerfilUsuario.Operador, StatusUsuario.PrimeiroAcesso),
        // Administrador do CLIENTE: entra na contagem, é cobrado no login.
        Usuario.Criar("Carla Admin Cliente", "carla@exemplo.test", PerfilUsuario.AdministradorCliente, StatusUsuario.Ativo),
        // Operação interna: isentos.
        Usuario.Criar("Diego Suporte", "diego@exemplo.test", PerfilUsuario.Suporte, StatusUsuario.Ativo),
        // Inativo: não faz login, então não entra no denominador.
        Usuario.Criar("Elis Inativa", "elis@exemplo.test", PerfilUsuario.Operador, StatusUsuario.Inativo));

    await db.SaveChangesAsync();
}


internal sealed record PublicarRequisicao(string ConteudoHtml, Guid PublicadoPor);
internal sealed record LoginRequisicao(Guid UsuarioId);
internal sealed record AceiteRequisicao(Guid UsuarioId);
