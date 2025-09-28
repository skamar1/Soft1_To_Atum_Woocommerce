<?php

/**
 * Database Configuration
 * Ρυθμίσεις σύνδεσης βάσης δεδομένων
 */

return [
    'default' => [
        'host' => $_ENV['DB_HOST'] ?? 'localhost',
        'port' => $_ENV['DB_PORT'] ?? 3306,
        'database' => $_ENV['DB_NAME'] ?? 'softone_atum_sync',
        'username' => $_ENV['DB_USER'] ?? 'root',
        'password' => $_ENV['DB_PASS'] ?? '',
        'charset' => 'utf8mb4',
        'collation' => 'utf8mb4_unicode_ci',
        'options' => [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES => false,
            PDO::MYSQL_ATTR_INIT_COMMAND => "SET NAMES utf8mb4"
        ]
    ],

    // Connection Pool Settings
    'pool' => [
        'max_connections' => 10,
        'timeout' => 30,
        'retry_attempts' => 3
    ],

    // Performance Settings
    'query_log' => false,
    'slow_query_threshold' => 1000, // milliseconds
    'cache_queries' => true
];