DROP PROCEDURE IF EXISTS MigrateWalkaroundChecklist;

DELIMITER $$

CREATE PROCEDURE MigrateWalkaroundChecklist()
BEGIN
    DECLARE isDone INT DEFAULT 0;
    DECLARE currentRecordId INT;
    DECLARE currentJson TEXT;
    DECLARE newJson TEXT;

    DECLARE walkaroundCursor CURSOR FOR
SELECT id, checklist_json FROM walkaround_checks;

DECLARE CONTINUE HANDLER FOR NOT FOUND SET isDone = 1;

OPEN walkaroundCursor;

conversionLoop: LOOP
        FETCH walkaroundCursor INTO currentRecordId, currentJson;

        IF isDone THEN
            LEAVE conversionLoop;
END IF;

        SET newJson = '[]';

BEGIN
            DECLARE itemIndex INT DEFAULT 0;
            DECLARE totalItems INT;
            DECLARE itemKey VARCHAR(200);
            DECLARE itemValue VARCHAR(10);
            DECLARE itemState VARCHAR(20);
            DECLARE itemAction VARCHAR(20);

            SET totalItems = JSON_LENGTH(currentJson);

            WHILE itemIndex < totalItems DO
                SET itemKey = JSON_UNQUOTE(
                    JSON_EXTRACT(JSON_KEYS(currentJson),
                    CONCAT('$[', itemIndex, ']'))
                );

                SET itemValue = JSON_UNQUOTE(
                    JSON_EXTRACT(currentJson, CONCAT('$.', itemKey))
                );

                IF itemValue = 'Pass' THEN
                    SET itemState  = 'Good';
                    SET itemAction = 'None';
ELSE
                    SET itemState  = 'Defect';
                    SET itemAction = 'RequiresGarage';
END IF;

                SET newJson = JSON_ARRAY_APPEND(
                    newJson,
                    '$',
                    JSON_OBJECT(
                        'item',        itemKey,
                        'state',       itemState,
                        'actionTaken', itemAction,
                        'note',        NULL
                    )
                );

                SET itemIndex = itemIndex + 1;
END WHILE;
END;

UPDATE walkaround_checks
SET checklist_json = newJson
WHERE id = currentRecordId;

END LOOP;

CLOSE walkaroundCursor;
END$$

DELIMITER ;

CALL MigrateWalkaroundChecklist();


-- Purpose   : Cria a tabela walkaround_photos para armazenar os metadados
--             das fotografias registradas durante os walkaround checks.
--             Os arquivos físicos ficam no Railway Bucket.
--             Este script é idempotente: usa IF NOT EXISTS para ser seguro
--             de executar mais de uma vez sem erro.
-- Affected  : walkaround_checks (FK), checklist_items (FK)
-- Run on    : Ambiente local primeiro, depois Railway MySQL via variável de ambiente

CREATE TABLE IF NOT EXISTS walkaround_photos (
                                                 id                INT             NOT NULL AUTO_INCREMENT,
                                                 walkaround_id     INT             NOT NULL,
                                                 checklist_item_id INT             NOT NULL,
                                                 driver_id         INT             NOT NULL,
                                                 vehicle_id        INT             NOT NULL,
                                                 storage_key       VARCHAR(500)    NOT NULL,
    file_size_kb      INT             NOT NULL,
    taken_at          DATETIME        NOT NULL,
    created_at        DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (id),

    CONSTRAINT fk_walkaround_photo_check
    FOREIGN KEY (walkaround_id)
    REFERENCES walkaround_checks (id)
    ON DELETE CASCADE,

    CONSTRAINT fk_walkaround_photo_checklist_item
    FOREIGN KEY (checklist_item_id)
    REFERENCES checklist_items (id)

    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;