[← voltar ao início](../README.md) · [← 01 — O problema](01-problema.md)

# 02 — Regras de negócio

As regras abaixo foram escritas para serem lidas por três públicos ao mesmo tempo: quem desenvolve, quem testa e quem é dono do documento legal. Cada uma tem a justificativa junto — a experiência é que regra sem porquê vira regra negociável na primeira pressão de prazo.

---

### RN01 — Nenhuma versão publicada é editável

Nenhuma versão do Termo de Uso pode ser editada pela interface, **inclusive a vigente**. Todas são consultáveis apenas em modo leitura.

Redigir um texto novo sempre gera uma **nova versão**. A versão anterior permanece imutável no histórico.

> **Por quê:** o registro de aceite aponta para uma versão específica. Alterar o texto de uma versão já aceita faria o usuário constar como tendo concordado com algo que nunca leu. Isso invalida o aceite.

---

### RN02 — Nova versão é redigida em editor de texto rico, com escopo fechado

A criação de uma versão acontece em um editor dentro da própria tela administrativa. Não há upload de arquivo.

A barra de ferramentas é **deliberadamente limitada**:

| Permitido | Não permitido |
|---|---|
| Seletor de bloco: Normal, Título 1, Título 2, Título 3 | Tabela |
| Negrito | Imagem |
| Lista ordenada e não ordenada | Cor de texto ou fundo |
| Recuo | Alinhamento |
| Link | Escolha de fonte ou tamanho |
| Quebra de linha (`Shift+Enter`) | |

> **Por quê:** o escopo foi guiado pelo documento real, não pelo que a biblioteca de editor sabe fazer. Três níveis de título existem porque o documento tem três níveis de título. Tabela e imagem não existem porque o documento não tem — e cada recurso a mais é mais superfície de sanitização, mais caso de renderização e mais jeito de o documento ficar feio sem ninguém perceber.

---

### RN03 — Salvar só habilita se houve mudança real, e publicar exige confirmação explícita

O botão de salvar permanece desabilitado enquanto o conteúdo do editor for **igual ao da versão vigente**. Não basta o campo estar preenchido.

Antes de publicar, uma confirmação informa o efeito em números: *"esta versão passará a ser exigida no próximo login de N usuários"*.

> **Por quê:** esta é a regra mais importante do conjunto e a que mais precisa da justificativa junto, porque parece fricção gratuita.
>
> No fluxo anterior, publicar exigia **escolher um arquivo no disco** — um ato deliberado, impossível de fazer por distração. Com o editor pré-preenchido com a versão vigente, o caminho virou abrir → digitar → salvar. A barreira sumiu junto com o upload.
>
> E o custo do erro é alto: publicar sem querer **força reaceite de toda a base de usuários**. Não existe desfazer — a única correção seria publicar outra versão, forçando um segundo reaceite para consertar o primeiro.
>
> A fricção foi recriada de propósito, no ponto certo: comparação de conteúdo (não de "houve digitação") mais confirmação que diz o tamanho do estrago.

---

### RN04 — Publicar troca a versão vigente em uma operação só

Ao publicar, o sistema cria a nova versão marcada como vigente e remove a marcação da anterior, **na mesma transação**. A anterior passa a ser consultável apenas em leitura.

> **Por quê:** a operação não pode ficar pela metade. Duas versões vigentes ao mesmo tempo, ou nenhuma, são estados que a aplicação inteira assume que não existem.

---

### RN05 — A nova versão é exigida a partir do próximo login

Usuário com versão defasada não conclui o login sem registrar o aceite.

**Sessão ativa no momento da publicação não é interrompida.** O aceite é cobrado no login seguinte.

> **Por quê:** publicar um documento legal é um ato administrativo; derrubar um usuário do meio de uma tarefa é um incidente. Conformidade não justifica perda de trabalho em andamento — a diferença entre cobrar agora e cobrar no próximo login é de horas, e o custo do "agora" recai sobre quem não tem nada a ver com a publicação.

---

### RN06 — A listagem mostra a contagem de aceites pendentes por versão

Para cada versão, a listagem exibe "X de Y usuários", onde X é quantos ainda **não** aceitaram aquela versão e Y é a base considerada.

> **Por quê:** é a única resposta rápida para "a publicação pegou?". Sem isso, saber se a nova versão está circulando exige consulta ao banco.

---

### RN07 — A lista de pendentes abre só pelo ícone, e só na versão vigente

A lista de usuários pendentes abre **pelo ícone** da linha da versão vigente. A linha inteira e o texto da contagem **não** são clicáveis. Versões antigas não oferecem o ícone.

A lista mostra Nome, E-mail e Cliente — sem documento fiscal e sem perfil.

> **Por quê:** linha inteira clicável numa tela que também tem "visualizar versão" gera clique errado. E pendência em versão antiga não é acionável: quem não aceitou a versão de março não precisa aceitá-la, precisa aceitar a vigente. Oferecer a lista ali seria oferecer uma ação que não leva a lugar nenhum.
>
> As colunas foram cortadas ao mínimo necessário para identificar e contatar a pessoa. Documento fiscal e perfil não ajudam nessa tarefa e são dado a mais exposto numa tela que não precisa deles.

---

### RN08 — A elegibilidade ao aceite é uma regra só, usada em todos os lugares

A definição de "quem está sujeito ao aceite" — quais perfis são isentos e quais status de usuário entram na conta — é **única**, compartilhada entre a barreira do login e a contagem da tela administrativa.

Perfis internos de operação da plataforma são isentos. **Administrador do lado do cliente não é isento.**

> **Por quê:** são dois lugares perguntando a mesma coisa. Com duas definições, elas divergem — e o sintoma é péssimo: a tela diz que todo mundo aceitou enquanto usuários continuam sendo barrados no login, ou o contrário.
>
> Isso não era risco hipotético. A especificação inicial de uma das telas registrou dois perfis isentos quando na prática eram três. A regra única resolve por construção, não por disciplina.

---

## Como estas regras viraram teste

Cada RN acima tem pelo menos um cenário correspondente em [06 — Critérios de aceite e QA](06-qa.md), incluindo os caminhos não-felizes. As regras que mais geraram cenário foram a RN03 (quatro cenários) e a RN07 (dois), justamente as que descrevem o que **não** deve acontecer.

---

[← 01 — O problema](01-problema.md) · [→ 03 — Decisões técnicas](03-decisoes-tecnicas.md)
