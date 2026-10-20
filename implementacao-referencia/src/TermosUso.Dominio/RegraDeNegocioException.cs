namespace TermosUso.Dominio;

/// <summary>
/// Violação de uma regra de negócio do domínio. Separada de <see cref="ArgumentException"/>
/// de propósito: a borda da aplicação traduz esta exceção em 422, enquanto erro de argumento
/// é defeito de programação e vira 500. São públicos diferentes — um é o usuário, o outro é
/// quem mantém o código.
/// </summary>
public sealed class RegraDeNegocioException : Exception
{
    public string Regra { get; }

    public RegraDeNegocioException(string regra, string mensagem) : base(mensagem)
    {
        Regra = regra;
    }
}
