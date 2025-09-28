-- SoftOne Go to ATUM Synchronization Database Schema
-- Created: 2025-09-27
-- Description: Tables για tracking sync operations, product mappings και statistics

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ========================================
-- Table: sync_logs
-- Purpose: Track sync execution history
-- ========================================
CREATE TABLE IF NOT EXISTS `sync_logs` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `store_id` varchar(50) NOT NULL COMMENT 'Store identifier από config',
  `sync_type` enum('manual','automatic','test') NOT NULL DEFAULT 'automatic',
  `start_time` datetime NOT NULL,
  `end_time` datetime DEFAULT NULL,
  `status` enum('running','completed','failed','cancelled') NOT NULL DEFAULT 'running',
  `products_processed` int(11) DEFAULT 0,
  `products_created` int(11) DEFAULT 0,
  `products_updated` int(11) DEFAULT 0,
  `products_errors` int(11) DEFAULT 0,
  `memory_usage` varchar(20) DEFAULT NULL COMMENT 'Peak memory usage',
  `execution_time` int(11) DEFAULT NULL COMMENT 'Total execution time in seconds',
  `error_message` text DEFAULT NULL,
  `summary_data` text DEFAULT NULL COMMENT 'JSON data με detailed summary',
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_store_start_time` (`store_id`, `start_time`),
  KEY `idx_status` (`status`),
  KEY `idx_sync_type` (`sync_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sync execution logs';

-- ========================================
-- Table: product_mappings
-- Purpose: Map SoftOne Go products to WooCommerce products
-- ========================================
CREATE TABLE IF NOT EXISTS `product_mappings` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `store_id` varchar(50) NOT NULL,
  `softone_item_id` varchar(100) NOT NULL COMMENT 'SoftOne Go ITEM.MTRL field',
  `softone_code` varchar(100) DEFAULT NULL COMMENT 'SoftOne Go ITEM.CODE field',
  `softone_sku` varchar(100) DEFAULT NULL COMMENT 'SoftOne Go ITEM.CODE1 field (barcode)',
  `woocommerce_product_id` int(11) DEFAULT NULL,
  `atum_inventory_id` int(11) DEFAULT NULL,
  `atum_location_id` int(11) NOT NULL,
  `last_sync_time` datetime DEFAULT NULL,
  `last_sync_log_id` int(11) DEFAULT NULL,
  `sync_status` enum('synced','pending','error','disabled') NOT NULL DEFAULT 'pending',
  `error_count` int(11) DEFAULT 0,
  `last_error` text DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unique_store_softone_item` (`store_id`, `softone_item_id`),
  KEY `idx_store_location` (`store_id`, `atum_location_id`),
  KEY `idx_woocommerce_product` (`woocommerce_product_id`),
  KEY `idx_atum_inventory` (`atum_inventory_id`),
  KEY `idx_sync_status` (`sync_status`),
  KEY `idx_softone_sku` (`softone_sku`),
  KEY `fk_sync_log` (`last_sync_log_id`),
  CONSTRAINT `fk_mapping_sync_log` FOREIGN KEY (`last_sync_log_id`) REFERENCES `sync_logs` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Product mapping between SoftOne Go and WooCommerce';

-- ========================================
-- Table: sync_statistics
-- Purpose: Store sync performance metrics
-- ========================================
CREATE TABLE IF NOT EXISTS `sync_statistics` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `date` date NOT NULL,
  `store_id` varchar(50) NOT NULL,
  `total_syncs` int(11) DEFAULT 0,
  `successful_syncs` int(11) DEFAULT 0,
  `failed_syncs` int(11) DEFAULT 0,
  `total_products_processed` int(11) DEFAULT 0,
  `total_products_created` int(11) DEFAULT 0,
  `total_products_updated` int(11) DEFAULT 0,
  `total_errors` int(11) DEFAULT 0,
  `avg_execution_time` decimal(10,2) DEFAULT NULL COMMENT 'Average execution time in seconds',
  `max_memory_usage` varchar(20) DEFAULT NULL,
  `api_calls_softone` int(11) DEFAULT 0,
  `api_calls_woocommerce` int(11) DEFAULT 0,
  `data_size_processed` bigint(20) DEFAULT 0 COMMENT 'Total data size in bytes',
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unique_date_store` (`date`, `store_id`),
  KEY `idx_date` (`date`),
  KEY `idx_store_id` (`store_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Daily sync statistics';

-- ========================================
-- Table: email_queue
-- Purpose: Queue email notifications
-- ========================================
CREATE TABLE IF NOT EXISTS `email_queue` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `sync_log_id` int(11) DEFAULT NULL,
  `email_type` enum('new_products','error_notification','daily_summary','test') NOT NULL,
  `recipient` varchar(255) NOT NULL,
  `subject` varchar(500) NOT NULL,
  `body` text NOT NULL,
  `status` enum('pending','sent','failed') NOT NULL DEFAULT 'pending',
  `attempts` int(11) DEFAULT 0,
  `max_attempts` int(11) DEFAULT 3,
  `scheduled_at` datetime NOT NULL,
  `sent_at` datetime DEFAULT NULL,
  `error_message` text DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_status_scheduled` (`status`, `scheduled_at`),
  KEY `idx_email_type` (`email_type`),
  KEY `fk_email_sync_log` (`sync_log_id`),
  CONSTRAINT `fk_email_sync_log` FOREIGN KEY (`sync_log_id`) REFERENCES `sync_logs` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Email notification queue';

-- ========================================
-- Table: api_cache
-- Purpose: Cache API responses για performance
-- ========================================
CREATE TABLE IF NOT EXISTS `api_cache` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `cache_key` varchar(255) NOT NULL,
  `cache_data` longtext NOT NULL,
  `expires_at` datetime NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unique_cache_key` (`cache_key`),
  KEY `idx_expires_at` (`expires_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='API response cache';

-- ========================================
-- Insert sample data for testing
-- ========================================

-- Sample sync log entry
INSERT INTO `sync_logs` (`store_id`, `sync_type`, `start_time`, `end_time`, `status`, `products_processed`, `products_created`, `products_updated`, `memory_usage`, `execution_time`, `summary_data`) VALUES
('store1', 'manual', '2025-09-27 10:00:00', '2025-09-27 10:05:30', 'completed', 150, 5, 12, '245MB', 330, '{"total_api_calls": 15, "new_products": ["prod1", "prod2"], "updated_products": ["prod3", "prod4"]}');

-- Sample email queue entry
INSERT INTO `email_queue` (`email_type`, `recipient`, `subject`, `body`, `scheduled_at`) VALUES
('test', 'admin@example.com', 'Test Email - Sync System', 'This is a test email από το sync system.', NOW());

-- ========================================
-- Views για εύκολη αναφορά
-- ========================================

-- View: Sync summary per store
CREATE OR REPLACE VIEW `sync_summary_by_store` AS
SELECT
    store_id,
    COUNT(*) as total_syncs,
    SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as successful_syncs,
    SUM(CASE WHEN status = 'failed' THEN 1 ELSE 0 END) as failed_syncs,
    AVG(execution_time) as avg_execution_time,
    SUM(products_processed) as total_products_processed,
    SUM(products_created) as total_products_created,
    SUM(products_updated) as total_products_updated,
    MAX(start_time) as last_sync_time
FROM sync_logs
GROUP BY store_id;

-- View: Recent sync activity
CREATE OR REPLACE VIEW `recent_sync_activity` AS
SELECT
    sl.id,
    sl.store_id,
    sl.sync_type,
    sl.start_time,
    sl.status,
    sl.products_processed,
    sl.execution_time,
    CASE
        WHEN sl.status = 'completed' THEN 'Success'
        WHEN sl.status = 'failed' THEN sl.error_message
        ELSE sl.status
    END as result
FROM sync_logs sl
ORDER BY sl.start_time DESC
LIMIT 100;

-- ========================================
-- Cleanup procedures
-- ========================================

DELIMITER //

-- Procedure: Clean old logs
CREATE PROCEDURE `CleanOldLogs`(IN retention_days INT)
BEGIN
    DECLARE exit handler for sqlexception
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    -- Delete old sync logs
    DELETE FROM sync_logs
    WHERE created_at < DATE_SUB(NOW(), INTERVAL retention_days DAY);

    -- Delete old statistics
    DELETE FROM sync_statistics
    WHERE date < DATE_SUB(CURDATE(), INTERVAL retention_days DAY);

    -- Delete sent emails older than 7 days
    DELETE FROM email_queue
    WHERE status = 'sent' AND sent_at < DATE_SUB(NOW(), INTERVAL 7 DAY);

    -- Clean expired cache
    DELETE FROM api_cache
    WHERE expires_at < NOW();

    COMMIT;
END //

DELIMITER ;

SET FOREIGN_KEY_CHECKS = 1;