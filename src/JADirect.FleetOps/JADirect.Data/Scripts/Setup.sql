-- =====================================================================
-- Script consolidado do schema, remontado a partir do SHOW CREATE TABLE
-- rodado em produção da JADirect em 2026-07-07.
-- Exclui as tabelas de backup daily_logs_backup_20260417 e
-- walkaround_checks_backup_pre_migration, que sao artefatos de migracao
-- e nao fazem parte do schema da aplicacao.
--
-- IMPORTANTE: testar este script em ambiente local antes de rodar
-- no banco MySQL do Railway do Truck Connection.
--
-- O Railway já provisiona o banco "railway" automaticamente ao criar
-- o serviço MySQL, o mesmo padrão usado hoje em produção da JADirect.
-- Não criar banco novo, apenas usar o que já existe.
-- =====================================================================
USE railway;

-- ---------------------------------------------------------------------
-- 1. tenants (sem dependencias)
-- ---------------------------------------------------------------------
CREATE TABLE `tenants` (
                           `id` int NOT NULL AUTO_INCREMENT,
                           `name` varchar(100) NOT NULL,
                           `whatsapp_manager_phone` varchar(20) NOT NULL,
                           `daily_log_deadline_hour` tinyint NOT NULL COMMENT 'Hora limite para registrar o Daily Log (fuso do tenant)',
                           `alert_driver_hour` tinyint NOT NULL COMMENT 'Hora do disparo do alerta ao motorista (fuso do tenant)',
                           `alert_manager_hour` tinyint NOT NULL COMMENT 'Hora do disparo do resumo ao gestor (fuso do tenant)',
                           `timezone` varchar(60) NOT NULL DEFAULT 'Europe/Dublin',
                           `is_active` tinyint(1) NOT NULL DEFAULT '1',
                           `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                           PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 2. users (sem dependencias)
-- ---------------------------------------------------------------------
CREATE TABLE `users` (
                         `id` int NOT NULL AUTO_INCREMENT,
                         `first_name` varchar(100) NOT NULL,
                         `surname` varchar(100) NOT NULL,
                         `email` varchar(150) NOT NULL,
                         `phone_number` varchar(20) NOT NULL,
                         `password_hash` varchar(255) NOT NULL,
                         `role_id` int NOT NULL,
                         `status_id` int NOT NULL,
                         `created_at` datetime NOT NULL,
                         PRIMARY KEY (`id`),
                         KEY `idx_user_email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 3. vehicles (sem dependencias)
-- ---------------------------------------------------------------------
CREATE TABLE `vehicles` (
                            `id` int NOT NULL AUTO_INCREMENT,
                            `registration_no` varchar(20) NOT NULL,
                            `manufacturer` varchar(50) NOT NULL,
                            `model` varchar(50) NOT NULL,
                            `vehicle_type_id` int NOT NULL,
                            `current_km` int NOT NULL DEFAULT '0',
                            `status_id` int NOT NULL,
                            `last_walkaround_at` datetime DEFAULT NULL,
                            `created_at` datetime NOT NULL,
                            PRIMARY KEY (`id`),
                            UNIQUE KEY `registration_no` (`registration_no`),
                            KEY `idx_vehicle_reg` (`registration_no`),
                            KEY `idx_vehicle_last_check` (`last_walkaround_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 4. checklist_items (sem dependencias)
-- ---------------------------------------------------------------------
CREATE TABLE `checklist_items` (
                                   `id` int NOT NULL AUTO_INCREMENT,
                                   `tenant_id` int NOT NULL DEFAULT '1',
                                   `vehicle_type_id` int NOT NULL,
                                   `category` varchar(100) NOT NULL,
                                   `label` varchar(200) NOT NULL,
                                   `sort_order` int NOT NULL DEFAULT '0',
                                   `is_active` tinyint NOT NULL DEFAULT '1',
                                   `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                   PRIMARY KEY (`id`),
                                   KEY `idx_checklist_tenant_type` (`tenant_id`,`vehicle_type_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Seed: Van (vehicle_type_id = 1) — 18 itens
INSERT INTO checklist_items (tenant_id, vehicle_type_id, category, label, sort_order) VALUES
                                                                                          (1, 1, 'Exterior & Structure', 'Tyres condition, inflation & fixings', 1),
                                                                                          (1, 1, 'Exterior & Structure', 'Lights, indicators & reflectors', 2),
                                                                                          (1, 1, 'Exterior & Structure', 'CVRT tax & insurance discs', 3),
                                                                                          (1, 1, 'Exterior & Structure', 'Bumpers & sideguards', 4),
                                                                                          (1, 1, 'Exterior & Structure', 'Number plates & marker plates', 5),
                                                                                          (1, 1, 'Exterior & Structure', 'Mirrors & windows', 6),
                                                                                          (1, 1, 'Exterior & Structure', 'Fuel cap secure', 7),
                                                                                          (1, 1, 'Engine & Fluids', 'Oil, water, washer & fuel levels', 8),
                                                                                          (1, 1, 'Engine & Fluids', 'Fuel / oil leaks', 9),
                                                                                          (1, 1, 'Engine & Fluids', 'Exhaust & smoke emission', 10),
                                                                                          (1, 1, 'Engine & Fluids', 'Wiring & battery', 11),
                                                                                          (1, 1, 'Cab & Controls', 'Windscreen wipers & washers', 12),
                                                                                          (1, 1, 'Cab & Controls', 'Horn', 13),
                                                                                          (1, 1, 'Cab & Controls', 'Drivers seat & seat belts', 14),
                                                                                          (1, 1, 'Cab & Controls', 'Steering controls & operation', 15),
                                                                                          (1, 1, 'Cab & Controls', 'Brake controls & operation', 16),
                                                                                          (1, 1, 'Cab & Controls', 'ABS/EBS & instruments/gauges', 17),
                                                                                          (1, 1, 'Cab & Controls', 'Load security & distribution', 18);

-- Seed: RigidTruck (vehicle_type_id = 2) — 25 itens
INSERT INTO checklist_items (tenant_id, vehicle_type_id, category, label, sort_order) VALUES
                                                                                          (1, 2, 'In-Cab Items', 'Mirrors', 1),
                                                                                          (1, 2, 'In-Cab Items', 'Windows', 2),
                                                                                          (1, 2, 'In-Cab Items', 'Driving controls', 3),
                                                                                          (1, 2, 'In-Cab Items', 'Safety belts', 4),
                                                                                          (1, 2, 'In-Cab Items', 'Windscreen washers and wipers', 5),
                                                                                          (1, 2, 'In-Cab Items', 'Horn', 6),
                                                                                          (1, 2, 'In-Cab Items', 'Tachograph', 7),
                                                                                          (1, 2, 'In-Cab Items', 'ABS and EBS warning lights', 8),
                                                                                          (1, 2, 'In-Cab Items', 'Instruments, gauges and warning devices', 9),
                                                                                          (1, 2, 'In-Cab Items', 'Air leaks and pressure', 10),
                                                                                          (1, 2, 'External Vehicle Checks', 'CRW, tax, insurance disc & driving licence', 11),
                                                                                          (1, 2, 'External Vehicle Checks', 'Tyres', 12),
                                                                                          (1, 2, 'External Vehicle Checks', 'Wheel condition and security', 13),
                                                                                          (1, 2, 'External Vehicle Checks', 'All lights and reflectors', 14),
                                                                                          (1, 2, 'External Vehicle Checks', 'Exhaust', 15),
                                                                                          (1, 2, 'External Vehicle Checks', 'Steps', 16),
                                                                                          (1, 2, 'External Vehicle Checks', 'Vehicle body, doors and curtains', 17),
                                                                                          (1, 2, 'External Vehicle Checks', 'Number plates and marker plates', 18),
                                                                                          (1, 2, 'External Vehicle Checks', 'Fuel level and leaks', 19),
                                                                                          (1, 2, 'External Vehicle Checks', 'Engine oil', 20),
                                                                                          (1, 2, 'External Vehicle Checks', 'Coolant, washer bottle and other levels', 21),
                                                                                          (1, 2, 'External Vehicle Checks', 'Load security and weight distribution', 22),
                                                                                          (1, 2, 'External Vehicle Checks', 'Air suspension', 23),
                                                                                          (1, 2, 'Checks With Engine Started', 'Steering', 24),
                                                                                          (1, 2, 'Checks With Engine Started', 'Brake operation', 25);

-- Seed: ArticulatedTruck (vehicle_type_id = 3) — 30 itens
INSERT INTO checklist_items (tenant_id, vehicle_type_id, category, label, sort_order) VALUES
                                                                                          (1, 3, 'In-Cab Items', 'Mirrors', 1),
                                                                                          (1, 3, 'In-Cab Items', 'Windows', 2),
                                                                                          (1, 3, 'In-Cab Items', 'Driving controls', 3),
                                                                                          (1, 3, 'In-Cab Items', 'Safety belts', 4),
                                                                                          (1, 3, 'In-Cab Items', 'Windscreen washers and wipers', 5),
                                                                                          (1, 3, 'In-Cab Items', 'Horn', 6),
                                                                                          (1, 3, 'In-Cab Items', 'Tachograph', 7),
                                                                                          (1, 3, 'In-Cab Items', 'ABS and EBS warning lights', 8),
                                                                                          (1, 3, 'In-Cab Items', 'Instruments, gauges and warning devices', 9),
                                                                                          (1, 3, 'In-Cab Items', 'Air leaks and pressure', 10),
                                                                                          (1, 3, 'External Vehicle Checks', 'CRW, tax, insurance disc & driving licence', 11),
                                                                                          (1, 3, 'External Vehicle Checks', 'Tyres', 12),
                                                                                          (1, 3, 'External Vehicle Checks', 'Wheel condition and security', 13),
                                                                                          (1, 3, 'External Vehicle Checks', 'All lights and reflectors', 14),
                                                                                          (1, 3, 'External Vehicle Checks', 'Exhaust', 15),
                                                                                          (1, 3, 'External Vehicle Checks', 'Steps', 16),
                                                                                          (1, 3, 'External Vehicle Checks', 'Vehicle body, doors and curtains', 17),
                                                                                          (1, 3, 'External Vehicle Checks', 'Number plates and marker plates', 18),
                                                                                          (1, 3, 'External Vehicle Checks', 'Fuel level and leaks', 19),
                                                                                          (1, 3, 'External Vehicle Checks', 'Engine oil', 20),
                                                                                          (1, 3, 'External Vehicle Checks', 'Coolant, washer bottle and other levels', 21),
                                                                                          (1, 3, 'External Vehicle Checks', 'Load security and weight distribution', 22),
                                                                                          (1, 3, 'External Vehicle Checks', 'Air suspension', 23),
                                                                                          (1, 3, 'Articulated & Trailer Checks', 'Susie connections', 24),
                                                                                          (1, 3, 'Articulated & Trailer Checks', 'Fifth wheel and locking devices', 25),
                                                                                          (1, 3, 'Articulated & Trailer Checks', 'Coupling / tow bar', 26),
                                                                                          (1, 3, 'Articulated & Trailer Checks', 'Landing legs and handle', 27),
                                                                                          (1, 3, 'Articulated & Trailer Checks', 'Trailer park brake', 28),
                                                                                          (1, 3, 'Checks With Engine Started', 'Steering', 29),
                                                                                          (1, 3, 'Checks With Engine Started', 'Brake operation', 30);

-- ---------------------------------------------------------------------
-- 5. walkaround_blocking_rules (sem dependencias)
-- ---------------------------------------------------------------------
CREATE TABLE `walkaround_blocking_rules` (
                                             `id` int NOT NULL AUTO_INCREMENT,
                                             `tenant_id` int NOT NULL DEFAULT '1',
                                             `item_state` varchar(50) NOT NULL,
                                             `action_taken` varchar(50) NOT NULL,
                                             `blocks_vehicle` tinyint NOT NULL DEFAULT '0',
                                             PRIMARY KEY (`id`),
                                             KEY `idx_blocking_rules_tenant` (`tenant_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO walkaround_blocking_rules
(tenant_id, item_state, action_taken, blocks_vehicle)
VALUES
    (1, 'Defect',    'Resolved',        0),
    (1, 'Defect',    'RequiresGarage',  0),
    (1, 'Attention', 'Resolved',        0),
    (1, 'Attention', 'RequiresGarage',  0);

-- ---------------------------------------------------------------------
-- 6. driver_assignments (depende de users, vehicles)
-- ---------------------------------------------------------------------
CREATE TABLE `driver_assignments` (
                                      `id` int NOT NULL AUTO_INCREMENT,
                                      `driver_id` int NOT NULL,
                                      `vehicle_id` int NOT NULL,
                                      `assignment_date` date NOT NULL,
                                      `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                      PRIMARY KEY (`id`),
                                      UNIQUE KEY `uq_driver_assignment_per_day` (`driver_id`,`assignment_date`),
                                      UNIQUE KEY `uq_vehicle_assignment_per_day` (`vehicle_id`,`assignment_date`),
                                      KEY `idx_driver_assignments_date` (`assignment_date`),
                                      CONSTRAINT `fk_driver_assignment_user` FOREIGN KEY (`driver_id`) REFERENCES `users` (`id`),
                                      CONSTRAINT `fk_driver_assignment_vehicle` FOREIGN KEY (`vehicle_id`) REFERENCES `vehicles` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 7. daily_logs (depende de users, vehicles, driver_assignments)
-- ---------------------------------------------------------------------
CREATE TABLE `daily_logs` (
                              `id` int NOT NULL AUTO_INCREMENT,
                              `log_date` datetime NOT NULL,
                              `user_id` int NOT NULL,
                              `vehicle_id` int NOT NULL,
                              `assignment_id` int DEFAULT NULL,
                              `deliveries` int NOT NULL DEFAULT '0',
                              `collections` int NOT NULL DEFAULT '0',
                              `returns` int NOT NULL DEFAULT '0',
                              `current_odometer` int DEFAULT NULL,
                              `notes` text,
                              `created_at` datetime NOT NULL,
                              PRIMARY KEY (`id`),
                              UNIQUE KEY `uq_daily_log_user_date` (`user_id`,`log_date`),
                              KEY `vehicle_id` (`vehicle_id`),
                              KEY `fk_daily_log_assignment` (`assignment_id`),
                              CONSTRAINT `daily_logs_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
                              CONSTRAINT `fk_daily_log_assignment` FOREIGN KEY (`assignment_id`) REFERENCES `driver_assignments` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 8. walkaround_checks (depende de users, vehicles, driver_assignments)
-- ---------------------------------------------------------------------
CREATE TABLE `walkaround_checks` (
                                     `id` int NOT NULL AUTO_INCREMENT,
                                     `check_date` datetime NOT NULL,
                                     `user_id` int NOT NULL,
                                     `vehicle_id` int NOT NULL,
                                     `assignment_id` int DEFAULT NULL,
                                     `odometer` int NOT NULL,
                                     `checklist_json` mediumtext NOT NULL,
                                     `has_defect` tinyint(1) NOT NULL DEFAULT '0',
                                     `status` varchar(20) NOT NULL DEFAULT 'Completed',
                                     `defect_notes` text,
                                     `latitude` decimal(10,8) DEFAULT NULL,
                                     `longitude` decimal(11,8) DEFAULT NULL,
                                     PRIMARY KEY (`id`),
                                     KEY `user_id` (`user_id`),
                                     KEY `vehicle_id` (`vehicle_id`),
                                     KEY `idx_check_date` (`check_date`),
                                     KEY `fk_walkaround_check_assignment` (`assignment_id`),
                                     CONSTRAINT `fk_walkaround_check_assignment` FOREIGN KEY (`assignment_id`) REFERENCES `driver_assignments` (`id`) ON DELETE SET NULL,
                                     CONSTRAINT `walkaround_checks_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
                                     CONSTRAINT `walkaround_checks_ibfk_2` FOREIGN KEY (`vehicle_id`) REFERENCES `vehicles` (`id`),
                                     CONSTRAINT `walkaround_checks_chk_1` CHECK ((`odometer` >= 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 9. walkaround_photos (depende de walkaround_checks, checklist_items)
-- ---------------------------------------------------------------------
CREATE TABLE `walkaround_photos` (
                                     `id` int NOT NULL AUTO_INCREMENT,
                                     `walkaround_id` int NOT NULL,
                                     `checklist_item_id` int NOT NULL,
                                     `driver_id` int NOT NULL,
                                     `vehicle_id` int NOT NULL,
                                     `storage_key` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                                     `file_size_kb` int NOT NULL,
                                     `taken_at` datetime NOT NULL,
                                     `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                     PRIMARY KEY (`id`),
                                     KEY `fk_walkaround_photo_check` (`walkaround_id`),
                                     KEY `fk_walkaround_photo_checklist_item` (`checklist_item_id`),
                                     CONSTRAINT `fk_walkaround_photo_check` FOREIGN KEY (`walkaround_id`) REFERENCES `walkaround_checks` (`id`) ON DELETE CASCADE,
                                     CONSTRAINT `fk_walkaround_photo_checklist_item` FOREIGN KEY (`checklist_item_id`) REFERENCES `checklist_items` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 10. driver_availability_periods (depende de users, tenants)
--     Tabela sem migration versionada no repositorio original,
--     remontada a partir do SHOW CREATE TABLE de producao.
-- ---------------------------------------------------------------------
CREATE TABLE `driver_availability_periods` (
                                               `id` int NOT NULL AUTO_INCREMENT,
                                               `tenant_id` int NOT NULL,
                                               `driver_id` int NOT NULL,
                                               `status_during_period` int NOT NULL COMMENT '4=OnLeave, 5=Sick',
                                               `availability_from_date` datetime NOT NULL,
                                               `availability_to_date` datetime NOT NULL,
                                               `reason` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                                               `auto_reactivate` tinyint(1) DEFAULT '1',
                                               `status` enum('active','expired','canceled') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Status do periodo: active=vigente, expired=finalizou naturalmente, canceled=cancelado pelo manager',
                                               `created_at` datetime DEFAULT CURRENT_TIMESTAMP,
                                               `updated_at` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                               PRIMARY KEY (`id`),
                                               KEY `fk_driver` (`driver_id`),
                                               KEY `idx_tenant_driver` (`tenant_id`,`driver_id`),
                                               KEY `idx_dates` (`availability_from_date`,`availability_to_date`),
                                               KEY `idx_tenant_date` (`tenant_id`,`availability_from_date`),
                                               CONSTRAINT `fk_driver` FOREIGN KEY (`driver_id`) REFERENCES `users` (`id`),
                                               CONSTRAINT `fk_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 11. whatsapp_message_logs (depende de tenants)
-- ---------------------------------------------------------------------
CREATE TABLE `whatsapp_message_logs` (
                                         `id` int NOT NULL AUTO_INCREMENT,
                                         `tenant_id` int NOT NULL,
                                         `user_id` int DEFAULT NULL COMMENT 'Preenchido quando o destinatario e um motorista',
                                         `message_type` varchar(30) NOT NULL COMMENT 'DriverAlert ou ManagerSummary',
                                         `phone_number` varchar(20) NOT NULL,
                                         `status` varchar(20) NOT NULL COMMENT 'Sent ou Failed',
                                         `meta_message_id` varchar(100) DEFAULT NULL COMMENT 'ID retornado pela Meta em caso de sucesso',
                                         `error_message` varchar(500) DEFAULT NULL COMMENT 'Detalhe do erro em caso de falha',
                                         `sent_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                         PRIMARY KEY (`id`),
                                         KEY `fk_log_tenant` (`tenant_id`),
                                         CONSTRAINT `fk_log_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 12. whatsapp_sessions (depende de tenants)
-- ---------------------------------------------------------------------
CREATE TABLE `whatsapp_sessions` (
                                     `id` int NOT NULL AUTO_INCREMENT,
                                     `tenant_id` int NOT NULL,
                                     `user_id` int NOT NULL,
                                     `phone_number` varchar(20) NOT NULL,
                                     `opened_at` datetime NOT NULL,
                                     `expires_at` datetime NOT NULL COMMENT 'Calculado como opened_at + 24 horas',
                                     PRIMARY KEY (`id`),
                                     KEY `fk_session_tenant` (`tenant_id`),
                                     CONSTRAINT `fk_session_tenant` FOREIGN KEY (`tenant_id`) REFERENCES `tenants` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- 13. Seed do tenant Truck Connection Limited
--     Ajuste os horarios de alerta antes de rodar, os valores abaixo
--     sao apenas placeholder (8h log deadline, 9h alerta driver, 9h manager).
-- ---------------------------------------------------------------------
INSERT INTO tenants
(name, whatsapp_manager_phone, daily_log_deadline_hour, alert_driver_hour, alert_manager_hour, timezone, is_active, created_at)
VALUES
    ('Truck Connection Limited', '00000000', 17, 17, 17, 'Europe/Dublin', 1, NOW());