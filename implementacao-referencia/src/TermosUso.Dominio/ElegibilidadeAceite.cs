namespace TermosUso.Dominio;

/// <summary>
/// Quem está sujeito ao aceite do Termo de Uso.
///
/// Esta classe existe por um motivo só: DOIS caminhos precisam dessa resposta, e eles não
/// podem divergir.
///
///   1. A barreira do login, que decide se o usuário conclui a autenticação.
///   2. A contagem de pendentes da tela administrativa, que mostra "X de Y ainda não aceitaram".
///
/// Com duas definições, elas divergem — e o sintoma é péssimo: a tela diz que todo mundo
/// aceitou enquanto usuários continuam sendo barrados no login, ou o contrário. Nenhum dos
/// dois lados parece errado isoladamente, então a investigação começa pelo lugar errado.
///
/// O risco não é hipotético. Numa primeira redação da especificação a lista de perfis isentos
/// saiu incompleta — faltava um dos três. Com definições duplicadas, essa divergência iria
/// para produção como uma contagem que nunca zera.
///
/// A regra fica aqui, e os dois caminhos consomem daqui.
/// </summary>
public static class ElegibilidadeAceite
{
    /// <summary>
    /// Perfis da operação interna da plataforma. São isentos porque o termo rege a relação
    /// com o cliente, e quem opera a plataforma não está do lado do cliente.
    ///
    /// <see cref="PerfilUsuario.AdministradorCliente"/> NÃO está aqui, e essa ausência é
    /// deliberada: "administrador" dito sem qualificação pode significar duas coisas muito
    /// diferentes, e a interpretação errada isentaria do termo legal exatamente o perfil que
    /// mais precisa aceitá-lo.
    /// </summary>
    public static readonly PerfilUsuario[] PerfisIsentos =
    [
        PerfilUsuario.SuperAdministrador,
        PerfilUsuario.Administrador,
        PerfilUsuario.Suporte
    ];

    /// <summary>
    /// Status que entram na conta. Inativo e Excluído ficam de fora: não fazem login, então
    /// não há o que cobrar, e contá-los inflaria o denominador da tela com gente que nunca
    /// vai aceitar — a contagem jamais zeraria.
    ///
    /// PrimeiroAcesso e RedefinicaoSenha entram porque ambos terminam em uma sessão
    /// autenticada, e o aceite é cobrado nesse caminho.
    /// </summary>
    public static readonly StatusUsuario[] StatusConsiderados =
    [
        StatusUsuario.Ativo,
        StatusUsuario.PrimeiroAcesso,
        StatusUsuario.RedefinicaoSenha
    ];

    /// <summary>
    /// ARRAY, e não HashSet ou IReadOnlySet — e isso não é descuido.
    ///
    /// Estes membros são consumidos dentro de consultas LINQ-to-SQL (ver
    /// TermosUsoRepositorio). O provedor traduz `array.Contains(coluna)` para `coluna IN (...)`
    /// ou `coluna = ANY(...)`; com os tipos de conjunto a tradução não é confiável, e o
    /// sintoma é uma exceção em tempo de EXECUÇÃO — que não aparece na compilação, nem num
    /// teste unitário que exercita a constante em memória, só quando a consulta roda.
    ///
    /// Em memória o conjunto seria a estrutura mais correta. Na fronteira com o banco, é a
    /// errada. A escolha de uma estrutura de dados depende de onde ela é consumida, não só
    /// do que ela representa.
    ///
    /// As listas têm três elementos. A diferença de custo entre varrer um array de três e
    /// consultar um hash é irrelevante; a diferença entre a consulta funcionar e explodir
    /// em produção não é.
    /// </summary>
    public static bool EstaSujeito(PerfilUsuario perfil, StatusUsuario status) =>
        !PerfisIsentos.Contains(perfil) && StatusConsiderados.Contains(status);
}
