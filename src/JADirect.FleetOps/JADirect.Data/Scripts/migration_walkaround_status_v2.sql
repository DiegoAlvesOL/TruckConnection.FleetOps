-- Purpose   : Adiciona a coluna status na tabela walkaround_checks para suportar
--             o conceito de rascunho (Draft) e inspeção concluída (Completed).
--             DEFAULT 'Completed' garante que todos os registros históricos
--             recebam um valor válido sem necessidade de UPDATE manual.
-- Affected  : walkaround_checks
-- Run on    : Ambiente local primeiro, depois Railway MySQL via variável de ambiente

ALTER TABLE walkaround_checks
    ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'Completed'
    AFTER has_defect;