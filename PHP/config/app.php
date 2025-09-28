<?php

/**
 * Application Configuration
 * Βασικές ρυθμίσεις για την εφαρμογή συγχρονισμού
 */

return [
    // Application Settings
    'app_name' => 'SoftOne Go to ATUM Sync',
    'version' => '1.0.0',
    'timezone' => 'Europe/Athens',

    // Debug & Logging
    'debug' => false,
    'log_level' => 'INFO', // DEBUG, INFO, WARNING, ERROR
    'log_retention_days' => 30,
    'max_log_file_size' => '100MB',

    // Performance Settings
    'memory_limit' => '512M',
    'execution_time_limit' => 3600, // 1 hour
    'batch_size' => 100,
    'api_timeout' => 30,
    'api_retry_attempts' => 3,
    'api_retry_delay' => 5, // seconds

    // Lock Settings (για αποφυγή παράλληλων εκτελέσεων)
    'lock_file' => __DIR__ . '/../logs/sync.lock',
    'lock_timeout' => 3600, // 1 hour

    // Email Settings
    'email_enabled' => true,
    'email_on_error' => true,
    'email_on_new_products' => true,
    'email_daily_summary' => true,
    'email_recipients' => [
        'admin@example.com',
        'manager@example.com'
    ],

    // SMTP Configuration
    'smtp' => [
        'host' => $_ENV['MAIL_HOST'] ?? 'localhost',
        'port' => $_ENV['MAIL_PORT'] ?? 587,
        'username' => $_ENV['MAIL_USERNAME'] ?? '',
        'password' => $_ENV['MAIL_PASSWORD'] ?? '',
        'encryption' => $_ENV['MAIL_ENCRYPTION'] ?? 'tls', // tls, ssl, or null
        'from_email' => $_ENV['MAIL_FROM'] ?? 'noreply@example.com',
        'from_name' => $_ENV['MAIL_FROM_NAME'] ?? 'SoftOne ATUM Sync',
        'timeout' => 30,
        'auth' => true
    ],

    // Notification Settings
    'notifications' => [
        'new_product_threshold' => 10, // Alert αν >10 νέα προϊόντα
        'error_threshold' => 5, // Alert αν >5 errors
        'performance_threshold' => 300 // Alert αν sync >5 minutes
    ],

    // API Rate Limiting
    'rate_limits' => [
        'softone_go' => [
            'requests_per_minute' => 60,
            'requests_per_hour' => 1000
        ],
        'woocommerce' => [
            'requests_per_minute' => 100,
            'requests_per_hour' => 2000
        ]
    ],

    // Cleanup Settings
    'cleanup' => [
        'old_logs' => true,
        'temp_files' => true,
        'old_statistics' => 90 // days
    ]
];