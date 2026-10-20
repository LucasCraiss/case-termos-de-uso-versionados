-- =============================================================================
-- Schema do Termo de Uso.
--
-- Este arquivo é o ponto central da implementação de referência: é aqui que moram
-- as invariantes que NÃO podem depender da aplicação se comportar bem.
--
-- A linha divisória usada em todo o projeto:
--   o que não admite exceção vai para o schema;
--   o que depende de julgamento fica no código.
--
-- "Só existe uma versão vigente" não é escolha, é definição  -> índice.
-- "O conteúdo não muda depois de publicado"                  -> trigger.
-- "Um usuário não assina duas vezes a mesma versão"          -> unique.
-- "O conteúdo tem no máximo N caracteres"                    -> código (é um número
--                                                               escolhido, pode mudar
--                                                               por decisão de produto).
-- =============================================================================

CREATE TABLE IF NOT EXISTS usuario (
    id          uuid        PRIMARY KEY,
    nome        text        NOT NULL,
    email       text        NOT NULL UNIQUE,

    -- 1 SuperAdministrador, 2 Administrador, 3 Suporte,
    -- 4 AdministradorCliente, 5 Operador.
    -- Os três primeiros são a operação interna e são ISENTOS do aceite.
    -- O 4 NÃO é isento, apesar do nome: é administrador do lado do cliente.
    perfil      integer     NOT NULL,

    -- 1 Ativo, 2 PrimeiroAcesso, 3 RedefinicaoSenha, 4 Inativo, 5 Excluido.
    -- Os três primeiros entram na contagem de elegíveis; os dois últimos não fazem
    -- login, então contá-los inflaria o denominador com gente que nunca vai aceitar.
    status      integer     NOT NULL
);


-- Código legível das versões: TU001, TU002...
-- SEQUENCE, e não MAX(codigo)+1 calculado na aplicação. Duas publicações concorrentes
-- leriam o mesmo máximo e gerariam o mesmo código. A publicação é uma operação rara e a
-- colisão seria improvável — mas a sequence não custa mais caro que o MAX+1, e remove uma
-- classe inteira de bug que seria irreprodutível em teste.
CREATE SEQUENCE IF NOT EXISTS termo_versao_codigo_seq START 1;


CREATE TABLE IF NOT EXISTS termo_versao (
    id              uuid        PRIMARY KEY,
    codigo          text        NOT NULL UNIQUE,

    -- O documento em si: HTML semântico JÁ SANITIZADO (allowlist: h1,h2,h3,p,br,strong,
    -- ul,ol,li,a; único atributo é href). Não há arquivo em bucket — esta coluna é a
    -- fonte da verdade da versão.
    conteudo_html   text        NOT NULL,

    publicado_em    timestamptz NOT NULL,

    -- Nulo na versão semeada na migração inicial: ela não foi publicada por ninguém,
    -- ela é a origem.
    publicado_por   uuid        NULL REFERENCES usuario (id),

    eh_vigente      boolean     NOT NULL DEFAULT false
);


-- -----------------------------------------------------------------------------
-- INVARIANTE 1 — no máximo uma versão vigente.
--
-- Índice único PARCIAL: a restrição vale só sobre as linhas com eh_vigente = true,
-- então todas as versões antigas convivem sem conflito e só pode haver uma atual.
--
-- Poderia ser um IF no serviço. Não é, porque precisa valer também para o UPDATE
-- manual às duas da manhã durante um incidente.
-- -----------------------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS ux_termo_versao_vigente
    ON termo_versao (eh_vigente)
    WHERE eh_vigente;


-- -----------------------------------------------------------------------------
-- INVARIANTE 2 — o conteúdo de uma versão publicada nunca muda.
--
-- Este é o ponto mais importante do schema inteiro.
--
-- termo_aceite aponta para uma linha daqui. Se o conteúdo pudesse ser alterado depois,
-- um usuário passaria a constar como tendo concordado com um texto que nunca leu. Isso
-- não é um bug de software: é a invalidação do aceite.
--
-- A ausência de setter na entidade C# protege contra o erro distraído. Ela não é a
-- garantia — uma migration mal escrita, um caminho de código novo ou um UPDATE direto
-- passariam por cima. A garantia é esta trigger.
--
-- Consequência aceita: um erro de conteúdo não tem conserto, só substituição por uma
-- versão nova — ao custo de reaceite de toda a base. É por isso que existe o teto de
-- tamanho em TermoVersao.TamanhoMaximoConteudo.
--
-- Repare que publicado_por e eh_vigente ficam de fora: eh_vigente PRECISA mudar quando
-- uma nova versão assume. Travar a linha inteira quebraria a própria publicação.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_termo_versao_conteudo_imutavel()
RETURNS trigger AS $$
BEGIN
    IF NEW.conteudo_html IS DISTINCT FROM OLD.conteudo_html THEN
        RAISE EXCEPTION
            'O conteúdo da versão % é imutável. Usuários já aceitaram este texto; '
            'alterá-lo faria constar que concordaram com algo que não leram. '
            'Para mudar o documento, publique uma nova versão.', OLD.codigo
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    IF NEW.codigo IS DISTINCT FROM OLD.codigo THEN
        RAISE EXCEPTION 'O código da versão % é imutável.', OLD.codigo
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_termo_versao_conteudo_imutavel ON termo_versao;
CREATE TRIGGER tg_termo_versao_conteudo_imutavel
    BEFORE UPDATE ON termo_versao
    FOR EACH ROW
    EXECUTE FUNCTION fn_termo_versao_conteudo_imutavel();


-- -----------------------------------------------------------------------------
-- A trilha de assinaturas. Append-only.
--
-- A entidade C# correspondente não expõe nenhum método além da fábrica: não existe
-- código capaz de alterar um aceite. Aqui a garantia é reforçada por trigger, porque
-- "nunca alterar nem apagar" também precisa valer fora da aplicação.
--
-- A FK para termo_versao é o que impede apagar uma versão que já foi assinada.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS termo_aceite (
    id                  uuid        PRIMARY KEY,
    usuario_id          uuid        NOT NULL REFERENCES usuario (id),

    -- Referência pelo id, nunca pelo código: o código é rótulo de apresentação, e o
    -- vínculo entre a assinatura e o documento assinado não pode depender dele.
    termo_versao_id     uuid        NOT NULL REFERENCES termo_versao (id),

    aceito_em           timestamptz NOT NULL,

    -- Nulo quando a origem não é conhecida — notadamente nos registros convertidos da
    -- migração inicial, em que o modelo antigo não guardava essa informação.
    ip_origem           text        NULL,

    -- INVARIANTE 3 — um usuário não assina duas vezes a mesma versão.
    -- Reenvio de requisição e duplo clique não podem virar dois registros.
    CONSTRAINT ux_termo_aceite_usuario_versao UNIQUE (usuario_id, termo_versao_id)
);

CREATE INDEX IF NOT EXISTS ix_termo_aceite_versao ON termo_aceite (termo_versao_id);


CREATE OR REPLACE FUNCTION fn_termo_aceite_append_only()
RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION
        'termo_aceite é append-only: uma assinatura registrada não é alterada nem removida.'
        USING ERRCODE = 'integrity_constraint_violation';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tg_termo_aceite_append_only ON termo_aceite;
CREATE TRIGGER tg_termo_aceite_append_only
    BEFORE UPDATE OR DELETE ON termo_aceite
    FOR EACH ROW
    EXECUTE FUNCTION fn_termo_aceite_append_only();
