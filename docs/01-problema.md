[← voltar ao início](../README.md)

# 01 — O problema

## A situação anterior

O Termo de Uso de uma plataforma SaaS B2B existia em três lugares ao mesmo tempo, e nenhum deles era um bom lugar.

### 1. O texto morava no código-fonte

O conteúdo do documento estava escrito direto dentro do sistema, como texto fixo. Isso significava:

- **Trocar uma palavra exigia deploy.** Um ajuste pedido pelo jurídico entrava na fila de desenvolvimento, passava por revisão de código e subia junto com o resto da release.
- **O jurídico não tinha autonomia.** Quem é dono do documento não conseguia alterá-lo. Quem conseguia alterá-lo não é dono dele.
- **Não havia como consultar versões anteriores.** A versão antiga simplesmente deixava de existir no momento em que a nova substituía o texto no código. O histórico, quando existia, era o histórico do repositório — útil para um desenvolvedor, inútil para uma auditoria.

### 2. O aceite era um campo, não um histórico

O sistema guardava, por usuário, **qual foi a última versão aceita**. Um campo. Cada novo aceite **sobrescrevia** o anterior.

A consequência prática: perguntas básicas de conformidade não tinham resposta.

> *"Quem aceitou a versão de março, e em que data?"*

Não havia como responder. A informação tinha sido sobrescrita pelo aceite seguinte. O que existia era um estado atual, não um registro.

Um aceite de termo é, na prática, uma assinatura. Guardar só a assinatura mais recente é o equivalente a jogar fora os contratos antigos toda vez que um novo é assinado.

### 3. O aceite só era cobrado no primeiro acesso

Usuário novo aceitava o termo ao entrar pela primeira vez. Usuário já ativo nunca mais era cobrado — **nem quando uma versão nova era publicada**.

Isso criava uma base de usuários em que a maioria estava vinculada a um documento que não era mais o vigente, sem que ninguém soubesse quem estava em qual versão. E, como visto no item anterior, sem como descobrir.

---

## A causa comum

Os três problemas vinham da mesma origem: **o documento não era tratado como um ativo com ciclo de vida própria.**

Não tinha versão, não tinha dono operacional, não tinha data de vigência, não tinha trilha de quem o aceitou. Era texto dentro de um programa.

A entrega não foi "criar uma tela de termos de uso". Foi dar ao documento o ciclo de vida que ele já deveria ter: publicação, vigência, histórico imutável e registro de aceite.

---

## O que estava fora de escopo desde o começo

Delimitar isso cedo evitou discussão depois:

- **Editar uma versão publicada.** Nunca. Texto novo sempre gera versão nova — e isso é decisão de produto, não limitação técnica. Ver a [decisão técnica nº 2](03-decisoes-tecnicas.md#adr-02--imutabilidade-garantida-pelo-banco-não-pelo-código).
- **Fluxo de aprovação interna antes da publicação.** A revisão do texto acontece fora do sistema; quando chega ao editor, já está aprovada.
- **Múltiplos documentos legais.** O escopo é o Termo de Uso. A modelagem não impede generalizar depois, mas não foi construída para isso.
- **Tradução ou versões por idioma.** Produto monolíngue no período.

---

## O que foi assumido

Premissas registradas na especificação, para que fiquem visíveis se um dia deixarem de valer:

| Premissa | Se deixar de valer |
|---|---|
| O documento tem no máximo três níveis de título | O editor precisa de mais um nível de cabeçalho |
| O volume de versões é baixo (ordem de unidades por ano) | A listagem precisa de paginação e busca |
| A base de usuários cabe em uma contagem síncrona | A contagem de pendentes precisa virar agregação assíncrona ou materializada |
| Publicação é um evento raro e planejado | O custo de forçar reaceite geral deixa de ser aceitável e precisa de versionamento com "mudança material" vs. "correção editorial" |

A última é a mais relevante: hoje **toda** publicação força reaceite. Um dia pode ser necessário distinguir uma correção de typo de uma mudança de cláusula. Essa distinção não foi construída — e foi deliberado não construí-la antes de haver necessidade real.

---

[→ 02 — Regras de negócio](02-regras-de-negocio.md)
