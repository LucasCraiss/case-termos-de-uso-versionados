# Termos de Uso versionados, com aceite rastreável

> Case de produto e engenharia: como transformar um documento legal que vivia dentro do código-fonte em um ativo versionado, imutável e auditável — sem derrubar ninguém do meio do trabalho.

**Autor:** Lucas Crais · **Papel:** Product / especificação, modelagem da solução e acompanhamento da entrega · **Período:** jul–ago de 2026

---

## Aviso

Este repositório é um **estudo de caso escrito do zero**. Ele descreve decisões de produto e de arquitetura de uma entrega que conduzi em uma plataforma SaaS B2B.

Não contém — e nunca conterá — código-fonte, documento, dado de cliente, credencial, captura de tela ou qualquer outro ativo da empresa. Nomes de empresa, produto, pessoas e sistemas foram omitidos de propósito. O que está aqui é raciocínio, não propriedade intelectual de terceiros.

---

## O problema

O Termo de Uso da plataforma estava **escrito direto dentro do código-fonte**. Trocar uma vírgula no documento legal exigia um desenvolvedor editar código e fazer deploy.

Pior que isso: o sistema guardava apenas *"qual foi a última versão que essa pessoa aceitou"*, e cada novo aceite **sobrescrevia** o anterior. Não existia histórico. Se alguém perguntasse "quem aceitou a versão de março, e quando?", não havia resposta.

E o aceite só era cobrado no primeiro acesso. Um usuário ativo há dois anos nunca era cobrado de novo — nem quando uma versão nova entrava em vigor.

Três problemas, uma causa comum: **o documento não era tratado como um ativo com ciclo de vida**.

---

## O que foi entregue

| | |
|---|---|
| **Versionamento** | Cada publicação cria uma versão nova. Nenhuma versão publicada é editável, nem a vigente. O histórico completo fica consultável em modo leitura. |
| **Autonomia do time jurídico/administrativo** | Nova versão é redigida em um editor de texto rico dentro do próprio admin. Zero deploy, zero desenvolvedor no caminho. |
| **Trilha de aceite** | Tabela append-only: quem aceitou, qual versão, quando, de qual IP. Nunca sobrescrita, nunca apagada. |
| **Cobrança no login** | Usuário ativo com versão defasada não conclui o login sem aceitar. Sessão aberta no momento da publicação **não** é interrompida. |
| **Segurança do conteúdo** | HTML sanitizado por allowlist **na ingestão**. Colagem vinda do Word/Google Docs é limpa antes de persistir. |
| **Visibilidade** | A tela de versões mostra, por versão, quantos usuários ainda não aceitaram — e permite abrir a lista. |

---

## As cinco decisões que sustentam a solução

### 1. A troca de upload por editor removeu uma barreira acidental — e ela precisou ser recriada de propósito

A versão anterior do fluxo publicava o termo via **upload de arquivo HTML**. Escolher um arquivo no disco é um ato deliberado: ninguém sobe um arquivo sem querer.

O editor rico pré-preenchido com a versão vigente mudou isso. O caminho para publicar virou **abrir → digitar → salvar**. E uma publicação acidental não é um erro pequeno: ela **força reaceite de toda a base de usuários**.

A fricção que o upload dava de graça foi reintroduzida de propósito, em duas camadas:

- o botão de salvar só habilita quando o conteúdo **mudou de fato** em relação à versão vigente — não basta estar preenchido;
- antes de publicar, uma confirmação explícita informa o efeito: *"esta versão passará a ser exigida no próximo login de N usuários"*.

Desfazer a edição até voltar ao texto original desabilita o botão de novo. A comparação é com o conteúdo, não com o fato de ter havido digitação.

### 2. Imutabilidade garantida pelo banco, não por convenção

O registro de aceite aponta para o **id da versão**. Se o conteúdo daquela versão pudesse ser alterado depois, o usuário passaria a constar como tendo aceitado um texto que nunca leu. Isso não é um bug de software — é um problema de validade jurídica do aceite.

Por isso a imutabilidade não ficou na camada de serviço, onde depende de todo mundo lembrar da regra. Ela foi para o schema: uma **trigger rejeita qualquer UPDATE na coluna de conteúdo**. O código da aplicação pode errar; o banco não deixa passar.

Mesma lógica para a invariante "só existe uma versão vigente": **índice único parcial**, não `if` no serviço.

### 3. Dois tetos de tamanho, e o motivo não é performance

Enquanto o documento vivia em arquivo, o limite de tamanho vinha de graça do limite de requisição do upload. Sem upload, esse teto natural desapareceu.

Foram definidos dois limites: um **na borda**, antes da sanitização (onde o lixo colado do Word ainda não foi removido), e um **no domínio**, depois da sanitização.

O motivo é **irreversibilidade**. Com UPDATE bloqueado por trigger, uma linha patológica gravada por acidente **não tem conserto**: a única saída seria publicar outra versão — forçando reaceite de toda a base só para corrigir um erro de colagem. O teto do domínio ficou em ~77× o tamanho do documento real, folgado de propósito: não é para barrar documento legítimo, é para pegar o patológico.

Performance não entrou na conta — a listagem usa projeção e nem lê a coluna de conteúdo.

### 4. A contagem da tela e a barreira do login usam a mesma regra, por construção

A tela administrativa mostra "X de Y usuários ainda não aceitaram". O login barra quem está pendente. São dois lugares perguntando a mesma coisa.

Se cada um tivesse a sua própria definição de "elegível" — quais perfis são isentos, quais status contam — elas divergiriam. E isso não era hipotético: a especificação inicial de uma das telas listou dois perfis isentos quando na prática eram três.

Os critérios foram extraídos para **constantes compartilhadas**, consumidas pelos dois caminhos. A contagem não pode divergir de quem é de fato barrado, porque é a mesma regra executando.

### 5. O rodapé mostra o termo que *aquele usuário* aceitou

O link "Termos de Uso" no rodapé não abre a versão vigente. Abre **a versão que aquele usuário aceitou**.

Parece detalhe. Não é: o documento que vincula o usuário é o que ele assinou, não o mais recente. Mostrar o vigente para quem ainda não o aceitou seria exibir um contrato que não se aplica a ele.

---

## Fora do caminho feliz

O que foi explicitamente tratado:

- **Colagem do Word/Google Docs** — allowlist de tags na ingestão: títulos, parágrafo, negrito, listas, item, link e quebra de linha. Único atributo aceito é o destino do link. Nada de `class`, `style` ou `id` chega ao banco.
- **Publicações concorrentes** — o código da versão vem de uma *sequence* do banco, nunca de `MAX+1`. Dois POSTs simultâneos não colidem.
- **Aceite duplicado** — restrição de unicidade por (usuário, versão).
- **Sessão ativa durante a publicação** — não é interrompida. O aceite é cobrado no login seguinte.
- **Primeiro acesso pelo link de e-mail** — fluxo preservado, sem regressão. Uma falha anterior nesse caminho (o aceite não aparecia) foi corrigida.
- **Perfis internos de operação** — isentos do aceite. Administrador do lado do cliente **não** é isento.

---

## Uma armadilha técnica que valeu a pena anotar

As constantes compartilhadas da decisão nº 4 foram definidas como **arrays**, não como conjuntos (`HashSet`/`IReadOnlySet`).

Não é descuido: o ORM não traduz esses tipos de forma confiável em `.Contains()` dentro de uma consulta LINQ-to-SQL. O sintoma é uma exceção em tempo de execução que chega ao cliente como erro de requisição — e só aparece quando a consulta roda, não na compilação.

A estrutura de dados "mais correta" em memória era a errada na fronteira com o banco.

---

## Evolução depois da entrega inicial

- **Notificação de nova versão** — ao publicar, os usuários sujeitos ao aceite são notificados por e-mail e pelo sino dentro do produto. Perfis internos ficam de fora.
- **Modal como "atualização"** — quem já era usuário recebe o termo apresentado como atualização, não como primeiro aceite. Texto e enquadramento diferentes para situações diferentes.
- **Trilha de auditoria** — a publicação de versão passou a aparecer na tela de auditoria do sistema.
- **Endpoint de exclusão de versão** — só backend, sem botão no admin, uso operacional. Recusa excluir a versão vigente e recusa excluir versão que já tem aceites registrados. Auditoria guarda um snapshot do conteúdo antes de excluir, porque não é exclusão lógica — é a única forma de recuperar o texto depois.

---

## Documentação detalhada

| Documento | Conteúdo |
|---|---|
| [01 — O problema](docs/01-problema.md) | A situação anterior em detalhe e por que cada parte dela quebrava |
| [02 — Regras de negócio](docs/02-regras-de-negocio.md) | RN01–RN08, como foram especificadas |
| [03 — Decisões técnicas](docs/03-decisoes-tecnicas.md) | Seis decisões em formato ADR: contexto, alternativas, escolha, consequência |
| [04 — Modelo de dados](docs/04-modelo-de-dados.md) | Diagrama ER e as invariantes garantidas no schema |
| [05 — Fluxos](docs/05-fluxos.md) | Login com aceite pendente, publicação de versão, consulta pelo rodapé |
| [06 — Critérios de aceite e QA](docs/06-qa.md) | Cenários felizes e não-felizes, como foram escritos para o time de QA |

---

## O que eu tiraria deste case

Duas coisas, se tivesse que resumir em duas linhas:

**Toda melhoria de usabilidade mexe no custo do erro.** Trocar upload por editor foi uma melhoria óbvia — e removeu, sem avisar, a única coisa que impedia uma publicação acidental de afetar a base inteira. Facilitar o caminho certo facilita o caminho errado junto.

**Invariante que importa não mora no serviço.** Imutabilidade do conteúdo, unicidade da versão vigente e não-duplicidade do aceite são verdades que precisam valer sempre, inclusive em um `UPDATE` manual no banco às duas da manhã durante um incidente. Essas regras foram para o schema. As que admitem exceção ficaram no código.
