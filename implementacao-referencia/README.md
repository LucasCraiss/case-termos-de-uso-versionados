[← voltar ao case](../README.md)

# Implementação de referência

Projeto .NET + PostgreSQL que implementa as decisões descritas no [case](../README.md). Roda, tem testes, e não é um esqueleto: as invariantes do schema são exercitadas contra um Postgres de verdade.

> **Escrito do zero para este repositório.** Resolve o mesmo problema com as mesmas decisões de arquitetura, mas nenhum arquivo aqui é derivado de código de terceiros.

---

## Rodar

```bash
docker compose up -d
dotnet test
```

Os testes de invariantes do banco são **pulados** se o Postgres não estiver de pé — e os de domínio, sanitização e serviço rodam de qualquer jeito. Para subir a API:

```bash
dotnet run --project src/TermosUso.Api
```

A API aplica o schema e semeia usuários de exemplo na primeira subida. A string de conexão vem de `TERMOS_DB`, com um padrão apontando para o container do `docker-compose.yml`.

---

## O que olhar primeiro

Se você tem cinco minutos, leia nesta ordem:

| Arquivo | Por quê |
|---|---|
| [`schema.sql`](src/TermosUso.Infraestrutura/schema.sql) | O coração. É onde moram as invariantes que não dependem da aplicação se comportar bem |
| [`InvariantesDoBancoTestes.cs`](tests/TermosUso.Testes/InvariantesDoBancoTestes.cs) | A prova. Ataca o banco com SQL direto, por fora do domínio, e mostra que ele se defende |
| [`ElegibilidadeAceite.cs`](src/TermosUso.Dominio/ElegibilidadeAceite.cs) | A regra única que impede a tela e o login de divergirem — e a armadilha de ORM que o tipo dela esconde |
| [`ComparadorDeConteudo.cs`](src/TermosUso.Aplicacao/ComparadorDeConteudo.cs) | A fricção deliberada: por que publicar ficou mais difícil de propósito |
| [`SanitizadorDeTermo.cs`](src/TermosUso.Aplicacao/SanitizadorDeTermo.cs) | Allowlist na ingestão, e uma interação que só apareceu nos testes |

---

## Estrutura

```
src/
  TermosUso.Dominio/           entidades, invariantes de domínio, regra de elegibilidade
  TermosUso.Aplicacao/         casos de uso, sanitização, comparação de conteúdo
  TermosUso.Infraestrutura/    EF Core, repositório e schema.sql
  TermosUso.Api/               endpoints HTTP
tests/
  TermosUso.Testes/            domínio e serviço em memória + invariantes contra Postgres
```

A dependência aponta sempre para dentro: a API conhece aplicação e infraestrutura, a infraestrutura conhece o domínio, e o domínio não conhece ninguém.

---

## Onde cada garantia mora

Esta é a decisão de projeto mais importante, e a tabela resume por que cada coisa está onde está.

| Garantia | Onde | Por quê ali |
|---|---|---|
| Conteúdo de versão publicada nunca muda | **Trigger** no Postgres | Precisa valer para o `UPDATE` manual durante um incidente, não só para o código bem-comportado |
| No máximo uma versão vigente | **Índice único parcial** | É definição, não política. Parcial porque só restringe as linhas vigentes — senão o histórico seria impossível |
| Um usuário não assina duas vezes a mesma versão | **Restrição UNIQUE** | Duplo clique e reenvio de requisição não podem virar dois registros |
| Aceite nunca é alterado nem removido | **Trigger** + entidade sem métodos | A classe C# não oferece o que chamar; o banco recusa quem tenta por fora |
| Versão assinada não pode ser apagada | **Chave estrangeira** | Apagá-la apagaria o documento que alguém assinou |
| Código de versão único sob concorrência | **Sequence** | `MAX+1` na aplicação colide em publicações simultâneas |
| Tamanho máximo do conteúdo | **Código** | É um número escolhido por julgamento; pode mudar por decisão de produto |
| "Mudou de verdade" antes de publicar | **Código** | Regra de produto, com normalização que depende do que conta como mudança |

A linha divisória: **o que não admite exceção vai para o schema; o que depende de julgamento fica no código.**

---

## Duas coisas que os testes ensinaram

**`KeepChildNodes` trata toda tag removida igual.** Ele existe para que colar um parágrafo embrulhado em `<div>` mantenha o parágrafo em vez de apagá-lo junto com o embrulho. Só que ele faz o mesmo com `<script>`: a tag some, o corpo vira texto, e o leitor passa a ver `roubar()` escrito no meio do termo. Não é falha de segurança — nada executa — mas é lixo visível num documento legal, e com a trigger de imutabilidade esse lixo seria permanente. A solução foi uma passada prévia que descarta a subárvore inteira dos elementos cujo conteúdo nunca deve virar texto. Os dois comportamentos precisam coexistir.

**A estrutura de dados certa depende de onde ela é consumida.** As listas de perfis isentos e status considerados são arrays, não `HashSet`. Em memória o conjunto seria mais correto. Mas elas são usadas dentro de consultas LINQ-to-SQL, onde `array.Contains(coluna)` traduz e o conjunto não traduz de forma confiável — e o sintoma é uma exceção em tempo de execução, invisível na compilação e invisível em qualquer teste que exercite a constante em memória. Há um teste que afirma o tipo, e outro que roda a consulta contra o Postgres: é a única forma de pegar isso antes da produção.

---

## O que não está coberto

Honestidade sobre as lacunas, pelo mesmo motivo que o case tem a seção equivalente:

- **A API não tem autenticação.** O endpoint de login recebe um identificador e segue. Autenticação não é o assunto aqui; o que importa é **quando** a verificação do aceite acontece — depois de validar credenciais, antes de liberar a sessão.
- **Não há autorização nos endpoints administrativos.** Numa aplicação real, publicar versão e listar pendentes seriam restritos aos perfis internos.
- **O schema é aplicado direto, não por migrations versionadas.** Serve para `docker compose up` e `dotnet run` bastarem; não serve para evoluir um banco em produção.
- **Não há interface.** O editor rico, o modal de leitura e a tela de versões são descritos no case e não implementados aqui.
- **Acessibilidade do conteúdo renderizado** não foi tratada — a mesma dívida que o case registra.
