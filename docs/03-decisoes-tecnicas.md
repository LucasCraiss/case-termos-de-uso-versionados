[← voltar ao início](../README.md) · [← 02 — Regras de negócio](02-regras-de-negocio.md)

# 03 — Decisões técnicas

Seis decisões em formato enxuto de ADR: contexto, alternativas consideradas, escolha e consequência aceita.

---

## ADR-01 — O conteúdo vive no banco, não em arquivo

**Contexto.** O documento precisava sair do código-fonte. As opções eram guardá-lo como arquivo em object storage ou como coluna de texto no banco relacional.

**Alternativas.**

| Opção | A favor | Contra |
|---|---|---|
| Arquivo em object storage | Limite de tamanho vem de graça; conteúdo grande não pesa no banco | Duas fontes de verdade para sincronizar; transação de publicação deixa de ser atômica; imutabilidade depende de política do bucket |
| Coluna de texto no banco | Publicação vira uma transação só; imutabilidade e unicidade garantidas pelo schema; nada para sincronizar | Precisa de limite de tamanho explícito; conteúdo trafega em consultas se não houver cuidado |

**Decisão.** Coluna de texto no banco. A fonte da verdade da versão é a linha; não existe arquivo.

**Consequência aceita.** O limite de tamanho precisou ser criado à mão — ver [ADR-03](#adr-03--dois-tetos-de-tamanho-por-irreversibilidade-não-por-performance). A listagem usa projeção e não traz a coluna de conteúdo, que só é lida quando alguém abre uma versão específica.

---

## ADR-02 — Imutabilidade garantida pelo banco, não pelo código

**Contexto.** [RN01](02-regras-de-negocio.md#rn01--nenhuma-versão-publicada-é-editável) diz que versão publicada não muda. A pergunta é onde essa garantia mora.

**Alternativas.**

- **Na entidade de domínio.** Propriedades sem setter público, nenhum método de alteração. Protege contra o erro distraído, mas não contra um `UPDATE` direto no banco, uma migration mal escrita ou um caminho de código novo que contorne o domínio.
- **No banco.** Trigger que rejeita `UPDATE` na coluna de conteúdo.

**Decisão.** As duas, com a do banco sendo a que vale.

A entidade expõe apenas um método de fábrica e um método para desmarcar a versão como vigente. A tabela de aceites não expõe nada além do construtor — é append-only por design, não por acordo.

E a trigger rejeita `UPDATE` no conteúdo. Mesmo vindo de fora da aplicação.

**Por que o banco.** A garantia precisa valer **sempre**, inclusive no `UPDATE` manual às duas da manhã durante um incidente. O código da aplicação pode errar; a restrição no schema não é opcional para ninguém.

Mesma lógica aplicada à invariante "no máximo uma versão vigente": **índice único parcial**, não validação no serviço.

**Consequência aceita.** Corrigir um erro de conteúdo exige publicar nova versão — com o custo de reaceite geral. É exatamente por isso que existe a [ADR-03](#adr-03--dois-tetos-de-tamanho-por-irreversibilidade-não-por-performance).

---

## ADR-03 — Dois tetos de tamanho, por irreversibilidade, não por performance

**Contexto.** Com o documento em arquivo, o limite de tamanho vinha do limite de requisição do upload. Sem upload, esse teto desapareceu.

**Decisão.** Dois limites, em pontos diferentes do caminho:

| Onde | Quando | Valor |
|---|---|---|
| **Borda** (entrada da requisição) | **Antes** da sanitização, quando o lixo colado do Word ainda está lá | Maior, vindo de configuração |
| **Domínio** (criação da entidade) | **Depois** da sanitização | ~77× o tamanho do documento real |

**Por que dois.** Conteúdo colado de editores de texto chega com uma quantidade enorme de marcação descartável. Um limite único teria que ser frouxo o bastante para caber o lixo — e aí não limita nada do que interessa — ou apertado o bastante para barrar o conteúdo limpo legítimo.

**Por que não é sobre performance.** A listagem nem lê a coluna de conteúdo. O motivo é **irreversibilidade**: com `UPDATE` bloqueado pela [ADR-02](#adr-02--imutabilidade-garantida-pelo-banco-não-pelo-código), uma linha patológica gravada por acidente não tem conserto. A única saída seria publicar outra versão, forçando reaceite de toda a base para corrigir um erro de colagem.

**Por que o teto é tão folgado.** Não é para barrar documento legítimo. É para pegar o patológico — a colagem acidental do documento inteiro três vezes, o dump de HTML de uma página web. Apertar o número aumentaria a chance de barrar uma versão real do termo, que é o caso que não pode falhar.

---

## ADR-04 — Sanitização por allowlist na ingestão, não na renderização

**Contexto.** O conteúdo vem de um editor rico alimentado por colagem. Colar do Word ou do Google Docs traz `class`, `style`, `id`, elementos proprietários e — no pior caso — vetor de script.

**Alternativas.**

- **Sanitizar ao renderizar.** O banco guarda o que veio. Cada lugar que exibe o termo precisa lembrar de limpar. Esquecer um lugar é uma falha de segurança.
- **Sanitizar ao ingerir.** O banco só guarda HTML já limpo. Quem lê, lê conteúdo confiável.

**Decisão.** Na ingestão, com **allowlist** — não blocklist.

Passa: títulos de três níveis, parágrafo, negrito, lista ordenada, lista não ordenada, item de lista, link e quebra de linha. Único atributo aceito é o destino do link. Tudo o mais é removido.

**Por que allowlist.** Blocklist é uma aposta de que você pensou em todos os vetores. Allowlist é uma afirmação sobre o que o documento precisa — e o documento precisa de muito pouco.

**Por que na ingestão.** Combinado com a [ADR-02](#adr-02--imutabilidade-garantida-pelo-banco-não-pelo-código), o conteúdo armazenado é limpo **e** imutável. O que foi sanitizado uma vez continua sanitizado para sempre. Se a sanitização fosse na renderização, uma falha futura no sanitizador exporia todo o histórico retroativamente.

**Consequência aceita.** Se um dia o documento precisar de tabela, a mudança exige alterar a allowlist e republicar — versões antigas continuam sem tabela, porque o conteúdo delas já foi limpo. Isso é aceitável: versão antiga não deveria mudar mesmo.

---

## ADR-05 — Código da versão vem de sequence do banco

**Contexto.** Cada versão recebe um código legível e sequencial, usado só para exibição. A referência real entre aceite e versão é por identificador único, nunca pelo código.

**Alternativas.**

- **`MAX(codigo) + 1` na aplicação.** Simples e errado sob concorrência: duas publicações simultâneas leem o mesmo máximo e geram o mesmo código.
- **Sequence do banco.** Atômica por definição.

**Decisão.** Sequence.

**Observação honesta.** Publicação é uma operação rara — a chance de duas simultâneas é baixíssima. A escolha não foi por probabilidade, foi por custo: a sequence não é mais trabalhosa que o `MAX+1`, e remove uma classe inteira de bug que seria irreprodutível em teste e aparecia como código duplicado em produção.

---

## ADR-06 — A elegibilidade ao aceite é uma constante compartilhada

**Contexto.** Dois caminhos precisam saber quem está sujeito ao aceite: a barreira do login e a contagem de pendentes da tela administrativa. Ver [RN08](02-regras-de-negocio.md#rn08--a-elegibilidade-ao-aceite-é-uma-regra-só-usada-em-todos-os-lugares).

**Decisão.** Os critérios — perfis isentos e status de usuário considerados — foram extraídos para **constantes compartilhadas**, consumidas pelos dois caminhos. A contagem não pode divergir da barreira, porque é a mesma regra executando.

**O risco era real.** A especificação inicial de uma das telas registrou dois perfis isentos quando na prática eram três. Com definições duplicadas, essa divergência teria ido para produção e aparecido como uma contagem que nunca zerava.

### A armadilha de implementação

As constantes foram definidas como **arrays**, não como `HashSet`/`IReadOnlySet`.

Não é descuido. O ORM não traduz esses tipos de forma confiável em `.Contains()` dentro de uma consulta LINQ-to-SQL. O sintoma é uma exceção em tempo de execução que chega ao cliente como erro de requisição, e que só aparece quando a consulta roda — não na compilação, não no teste unitário que exercita a constante em memória.

A estrutura de dados "mais correta" em memória era a errada na fronteira com o banco. Vale como lembrete de que a escolha de uma estrutura de dados depende de onde ela é consumida, não só do que ela representa.

---

[← 02 — Regras de negócio](02-regras-de-negocio.md) · [→ 04 — Modelo de dados](04-modelo-de-dados.md)
