namespace TermosUso.Dominio;

/// <summary>
/// Perfis. Os três primeiros são a operação interna da plataforma.
///
/// Persistidos como inteiro (padrão do EF Core para enum). A alternativa — gravar o nome como
/// texto — é mais legível no banco, mas obrigaria a comparar por string nas consultas de
/// elegibilidade, e aí a lista de perfis isentos existiria em duas formas. Duas formas da
/// mesma lista é exatamente o problema que <see cref="ElegibilidadeAceite"/> existe para evitar.
/// O mapeamento está documentado em `schema.sql`.
/// </summary>
public enum PerfilUsuario
{
    SuperAdministrador = 1,
    Administrador = 2,
    Suporte = 3,

    /// <summary>
    /// Administrador do lado do CLIENTE. Apesar do nome, não é operação interna e
    /// NÃO é isento do aceite — ver <see cref="ElegibilidadeAceite.PerfisIsentos"/>.
    /// </summary>
    AdministradorCliente = 4,

    Operador = 5
}

public enum StatusUsuario
{
    Ativo = 1,
    PrimeiroAcesso = 2,
    RedefinicaoSenha = 3,
    Inativo = 4,
    Excluido = 5
}

public sealed class Usuario
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public PerfilUsuario Perfil { get; private set; }
    public StatusUsuario Status { get; private set; }

    private Usuario() { }

    public static Usuario Criar(string nome, string email, PerfilUsuario perfil, StatusUsuario status)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome é obrigatório.", nameof(nome));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("E-mail é obrigatório.", nameof(email));

        return new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            Email = email,
            Perfil = perfil,
            Status = status
        };
    }

    /// <summary>
    /// Conveniência para o caminho em memória. A consulta ao banco usa os mesmos arrays
    /// diretamente, por tradutibilidade — ver <see cref="ElegibilidadeAceite"/>.
    /// </summary>
    public bool EstaSujeitoAoAceite() =>
        ElegibilidadeAceite.EstaSujeito(Perfil, Status);
}
