using Microsoft.EntityFrameworkCore;
using TermosUso.Dominio;

namespace TermosUso.Infraestrutura;

public sealed class TermosUsoDbContext(DbContextOptions<TermosUsoDbContext> options) : DbContext(options)
{
    public DbSet<TermoVersao> Versoes => Set<TermoVersao>();
    public DbSet<TermoAceite> Aceites => Set<TermoAceite>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<TermoVersao>(e =>
        {
            e.ToTable("termo_versao");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id");
            e.Property(v => v.Codigo).HasColumnName("codigo").IsRequired();
            e.Property(v => v.ConteudoHtml).HasColumnName("conteudo_html").IsRequired();
            e.Property(v => v.PublicadoEm).HasColumnName("publicado_em");
            e.Property(v => v.PublicadoPor).HasColumnName("publicado_por");
            e.Property(v => v.EhVigente).HasColumnName("eh_vigente");

            e.HasIndex(v => v.Codigo).IsUnique();

            // O índice único PARCIAL que garante "no máximo uma vigente" é criado em
            // schema.sql, não aqui. O EF sabe declará-lo, mas deixá-lo junto das outras
            // invariantes — a trigger de imutabilidade, a sequence — mantém num lugar só
            // tudo o que o banco garante sozinho, inclusive para quem lê o schema sem
            // passar pelo código C#.
        });

        modelo.Entity<TermoAceite>(e =>
        {
            e.ToTable("termo_aceite");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.UsuarioId).HasColumnName("usuario_id");
            e.Property(a => a.TermoVersaoId).HasColumnName("termo_versao_id");
            e.Property(a => a.AceitoEm).HasColumnName("aceito_em");
            e.Property(a => a.IpOrigem).HasColumnName("ip_origem");

            e.HasIndex(a => new { a.UsuarioId, a.TermoVersaoId }).IsUnique();
        });

        modelo.Entity<Usuario>(e =>
        {
            e.ToTable("usuario");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Nome).HasColumnName("nome").IsRequired();
            e.Property(u => u.Email).HasColumnName("email").IsRequired();

            // Enums como inteiro: ver a nota em PerfilUsuario sobre por que não como texto.
            e.Property(u => u.Perfil).HasColumnName("perfil").HasConversion<int>();
            e.Property(u => u.Status).HasColumnName("status").HasConversion<int>();
        });
    }
}
