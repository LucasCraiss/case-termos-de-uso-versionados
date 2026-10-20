using Microsoft.EntityFrameworkCore;
using Npgsql;
using TermosUso.Infraestrutura;

namespace TermosUso.Testes;

/// <summary>
/// Conexão com o Postgres do `docker compose up`.
///
/// Os testes que dependem dela são PULADOS quando não há banco, em vez de falharem:
/// uma suíte que quebra inteira em quem não subiu o container não é informativa, é
/// ruído. O README diz como ligar.
/// </summary>
public sealed class BancoDeTeste : IAsyncLifetime
{
    public const string StringDeConexaoPadrao =
        "Host=localhost;Port=5432;Database=termos_uso_testes;Username=postgres;Password=postgres";

    public bool Disponivel { get; private set; }
    public string StringDeConexao { get; private set; } = StringDeConexaoPadrao;

    public async Task InitializeAsync()
    {
        StringDeConexao =
            Environment.GetEnvironmentVariable("TERMOS_DB_TESTES") ?? StringDeConexaoPadrao;

        try
        {
            await CriarBancoSeNecessarioAsync();

            await using var conexao = new NpgsqlConnection(StringDeConexao);
            await conexao.OpenAsync();
            Disponivel = true;
        }
        catch (NpgsqlException)
        {
            Disponivel = false;
        }
        catch (System.Net.Sockets.SocketException)
        {
            Disponivel = false;
        }
    }

    private async Task CriarBancoSeNecessarioAsync()
    {
        var construtor = new NpgsqlConnectionStringBuilder(StringDeConexao);
        var nomeDoBanco = construtor.Database!;
        construtor.Database = "postgres";

        await using var conexao = new NpgsqlConnection(construtor.ConnectionString);
        await conexao.OpenAsync();

        await using var existe = conexao.CreateCommand();
        existe.CommandText = "SELECT 1 FROM pg_database WHERE datname = @nome";
        existe.Parameters.AddWithValue("nome", nomeDoBanco);

        if (await existe.ExecuteScalarAsync() is not null)
            return;

        await using var criar = conexao.CreateCommand();
        criar.CommandText = $"CREATE DATABASE \"{nomeDoBanco}\"";
        await criar.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Contexto novo com o schema aplicado e as tabelas vazias. Cada teste parte do zero:
    /// os testes aqui exercitam restrições do banco, e restrição violada aborta transação —
    /// estado residual entre testes viraria falha em cascata difícil de ler.
    /// </summary>
    public async Task<TermosUsoDbContext> NovoContextoLimpoAsync()
    {
        var opcoes = new DbContextOptionsBuilder<TermosUsoDbContext>()
            .UseNpgsql(StringDeConexao)
            .Options;

        var db = new TermosUsoDbContext(opcoes);

        var caminho = Path.Combine(AppContext.BaseDirectory, "schema.sql");
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(caminho));

        // TRUNCATE não dispara as triggers de linha, então limpa inclusive a tabela
        // append-only — que é append-only para a aplicação, não para o setup do teste.
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE termo_aceite, termo_versao, usuario RESTART IDENTITY CASCADE");
        await db.Database.ExecuteSqlRawAsync("ALTER SEQUENCE termo_versao_codigo_seq RESTART WITH 1");

        return db;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(nameof(ColecaoDeBanco))]
public sealed class ColecaoDeBanco : ICollectionFixture<BancoDeTeste>;
