-- =============================================================
-- migration_whatsapp_compliance_v1.sql
-- Purpose   : Cria a infraestrutura de banco para os alertas
--             automáticos de conformidade via WhatsApp.
-- Environment: Development — validar aqui antes do deploy.
-- Deploy note: Executar este script ANTES de fazer o deploy
--              da versão que contém esta feature em produção.
-- =============================================================

-- -------------------------------------------------------------
-- 1. Tabela de tenants
-- -------------------------------------------------------------
CREATE TABLE tenants (
                         id                      INT          NOT NULL AUTO_INCREMENT,
                         name                    VARCHAR(100) NOT NULL,
                         whatsapp_manager_phone  VARCHAR(20)  NOT NULL,
                         daily_log_deadline_hour TINYINT      NOT NULL COMMENT 'Hora limite para registrar o Daily Log (fuso do tenant)',
                         alert_driver_hour       TINYINT      NOT NULL COMMENT 'Hora do disparo do alerta ao motorista (fuso do tenant)',
                         alert_manager_hour      TINYINT      NOT NULL COMMENT 'Hora do disparo do resumo ao gestor (fuso do tenant)',
                         timezone                VARCHAR(60)  NOT NULL DEFAULT 'Europe/Dublin',
                         is_active               TINYINT(1)   NOT NULL DEFAULT 1,
                         created_at              DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                         PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Primeiro tenant: JA Direct
-- Substituir +353XXXXXXXXX pelo número real do Johnny antes do deploy em produção.
INSERT INTO tenants (
    name,
    whatsapp_manager_phone,
    daily_log_deadline_hour,
    alert_driver_hour,
    alert_manager_hour,
    timezone
)
VALUES (
           'JA Direct',
           '+353XXXXXXXXX',
           17,
           17,
           18,
           'Europe/Dublin'
       );

-- -------------------------------------------------------------
-- 2. Tabela de log de mensagens enviadas
-- -------------------------------------------------------------
CREATE TABLE whatsapp_message_logs (
                                       id              INT          NOT NULL AUTO_INCREMENT,
                                       tenant_id       INT          NOT NULL,
                                       user_id         INT          NULL     COMMENT 'Preenchido quando o destinatário é um motorista',
                                       message_type    VARCHAR(30)  NOT NULL COMMENT 'DriverAlert ou ManagerSummary',
                                       phone_number    VARCHAR(20)  NOT NULL,
                                       status          VARCHAR(20)  NOT NULL COMMENT 'Sent ou Failed',
                                       meta_message_id VARCHAR(100) NULL     COMMENT 'ID retornado pela Meta em caso de sucesso',
                                       error_message   VARCHAR(500) NULL     COMMENT 'Detalhe do erro em caso de falha',
                                       sent_at         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                       PRIMARY KEY (id),
                                       CONSTRAINT fk_log_tenant FOREIGN KEY (tenant_id)
                                           REFERENCES tenants(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- -------------------------------------------------------------
-- 3. Tabela de sessões WhatsApp (janela de 24h gratuita da Meta)
-- -------------------------------------------------------------
CREATE TABLE whatsapp_sessions (
                                   id           INT         NOT NULL AUTO_INCREMENT,
                                   tenant_id    INT         NOT NULL,
                                   user_id      INT         NOT NULL,
                                   phone_number VARCHAR(20) NOT NULL,
                                   opened_at    DATETIME    NOT NULL,
                                   expires_at   DATETIME    NOT NULL COMMENT 'Calculado como opened_at + 24 horas',
                                   PRIMARY KEY (id),
                                   CONSTRAINT fk_session_tenant FOREIGN KEY (tenant_id)
                                       REFERENCES tenants(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- -------------------------------------------------------------
-- Verificação: confirme que as três tabelas foram criadas.
-- -------------------------------------------------------------
SHOW TABLES LIKE 'tenants';
SHOW TABLES LIKE 'whatsapp_message_logs';
SHOW TABLES LIKE 'whatsapp_sessions';