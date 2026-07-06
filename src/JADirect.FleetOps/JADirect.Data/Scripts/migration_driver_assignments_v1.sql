-- Purpose   : Cria a tabela driver_assignments para registrar qual motorista
--             assumiu qual veículo em cada dia de operação.
--             A regra de encerramento é implícita pela data: o assignment
--             expira automaticamente à meia-noite, sem necessidade de coluna status.
--             Este script é idempotente: usa IF NOT EXISTS para ser seguro
--             de executar mais de uma vez sem erro.
-- Affected  : users (FK driver_id), vehicles (FK vehicle_id)
-- Run on    : Ambiente local primeiro, depois Railway MySQL via variável de ambiente

CREATE TABLE IF NOT EXISTS driver_assignments (
                                                  id               INT          NOT NULL AUTO_INCREMENT,
                                                  driver_id        INT          NOT NULL,
                                                  vehicle_id       INT          NOT NULL,
                                                  assignment_date  DATE         NOT NULL,
                                                  created_at       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                                  PRIMARY KEY (id),

    CONSTRAINT fk_driver_assignment_user
    FOREIGN KEY (driver_id)
    REFERENCES users (id),

    CONSTRAINT fk_driver_assignment_vehicle
    FOREIGN KEY (vehicle_id)
    REFERENCES vehicles (id),

    CONSTRAINT uq_driver_assignment_per_day
    UNIQUE (driver_id, assignment_date),

    CONSTRAINT uq_vehicle_assignment_per_day
    UNIQUE (vehicle_id, assignment_date),

    INDEX idx_driver_assignments_date (assignment_date)

    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;