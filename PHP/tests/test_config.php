<?php

/**
 * Configuration Test Script
 * Ελέγχει αν όλα τα configuration files είναι έγκυρα
 */

echo "🔧 SoftOne ATUM Sync - Configuration Test\n";
echo "==========================================\n\n";

$errors = [];
$warnings = [];

// Test 1: Check if configuration files exist
echo "📁 Checking configuration files...\n";

$configFiles = [
    'app.php' => __DIR__ . '/../config/app.php',
    'stores.php' => __DIR__ . '/../config/stores.php',
    'database.php' => __DIR__ . '/../config/database.php'
];

foreach ($configFiles as $name => $path) {
    if (file_exists($path)) {
        echo "  ✅ {$name} - Found\n";
    } else {
        $errors[] = "{$name} - Not found at {$path}";
        echo "  ❌ {$name} - Not found\n";
    }
}

// Test 2: Load and validate configurations
echo "\n🔍 Validating configuration content...\n";

try {
    // Load app config
    if (file_exists($configFiles['app.php'])) {
        $appConfig = require $configFiles['app.php'];

        if (!is_array($appConfig)) {
            $errors[] = "app.php - Must return an array";
        } else {
            $requiredKeys = ['app_name', 'log_level', 'batch_size', 'email_recipients'];
            foreach ($requiredKeys as $key) {
                if (!isset($appConfig[$key])) {
                    $warnings[] = "app.php - Missing recommended key: {$key}";
                }
            }
            echo "  ✅ app.php - Valid structure\n";
        }
    }

    // Load stores config
    if (file_exists($configFiles['stores.php'])) {
        $storesConfig = require $configFiles['stores.php'];

        if (!is_array($storesConfig)) {
            $errors[] = "stores.php - Must return an array";
        } else {
            $storeCount = 0;
            $enabledStores = 0;

            foreach ($storesConfig as $storeId => $storeConfig) {
                $storeCount++;

                if ($storeConfig['enabled'] ?? true) {
                    $enabledStores++;
                }

                // Check required sections
                $requiredSections = ['softone_go', 'atum', 'woocommerce'];
                foreach ($requiredSections as $section) {
                    if (!isset($storeConfig[$section])) {
                        $errors[] = "stores.php - Store '{$storeId}' missing section: {$section}";
                    }
                }

                // Check SoftOne Go config
                if (isset($storeConfig['softone_go'])) {
                    $requiredKeys = ['base_url', 'app_id', 'token', 's1code'];
                    foreach ($requiredKeys as $key) {
                        if (empty($storeConfig['softone_go'][$key])) {
                            $errors[] = "Store '{$storeId}' - Missing SoftOne Go: {$key}";
                        }
                    }
                }

                // Check ATUM config
                if (isset($storeConfig['atum'])) {
                    if (empty($storeConfig['atum']['location_id'])) {
                        $errors[] = "Store '{$storeId}' - Missing ATUM location_id";
                    }
                }

                // Check WooCommerce config
                if (isset($storeConfig['woocommerce'])) {
                    $requiredKeys = ['url', 'consumer_key', 'consumer_secret'];
                    foreach ($requiredKeys as $key) {
                        if (empty($storeConfig['woocommerce'][$key])) {
                            $errors[] = "Store '{$storeId}' - Missing WooCommerce: {$key}";
                        }
                    }
                }
            }

            echo "  ✅ stores.php - Found {$storeCount} stores ({$enabledStores} enabled)\n";
        }
    }

    // Load database config
    if (file_exists($configFiles['database.php'])) {
        $dbConfig = require $configFiles['database.php'];

        if (!is_array($dbConfig)) {
            $errors[] = "database.php - Must return an array";
        } else {
            if (!isset($dbConfig['default'])) {
                $errors[] = "database.php - Missing 'default' configuration";
            } else {
                $requiredKeys = ['host', 'database', 'username', 'password'];
                foreach ($requiredKeys as $key) {
                    if (!isset($dbConfig['default'][$key])) {
                        $errors[] = "database.php - Missing key: {$key}";
                    }
                }
            }
            echo "  ✅ database.php - Valid structure\n";
        }
    }

} catch (Exception $e) {
    $errors[] = "Configuration loading error: " . $e->getMessage();
}

// Test 3: Check environment variables
echo "\n🌍 Checking environment variables...\n";

$envFile = __DIR__ . '/../.env';
if (file_exists($envFile)) {
    echo "  ✅ .env file found\n";

    // Load .env file
    $lines = file($envFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
    $envVars = [];

    foreach ($lines as $line) {
        if (strpos($line, '=') !== false && strpos($line, '#') !== 0) {
            list($key, $value) = explode('=', $line, 2);
            $envVars[trim($key)] = trim($value);
        }
    }

    $requiredEnvVars = ['DB_HOST', 'DB_NAME', 'DB_USER', 'DB_PASS'];
    foreach ($requiredEnvVars as $var) {
        if (isset($envVars[$var]) && !empty($envVars[$var])) {
            echo "  ✅ {$var} - Set\n";
        } else {
            $warnings[] = "Environment variable {$var} not set";
            echo "  ⚠️  {$var} - Not set\n";
        }
    }
} else {
    $warnings[] = ".env file not found - using default values";
    echo "  ⚠️  .env file not found\n";
}

// Test 4: Check directories and permissions
echo "\n📁 Checking directories and permissions...\n";

$directories = [
    'logs' => __DIR__ . '/../logs',
    'config' => __DIR__ . '/../config',
    'src' => __DIR__ . '/../src'
];

foreach ($directories as $name => $path) {
    if (is_dir($path)) {
        if (is_writable($path)) {
            echo "  ✅ {$name} - Directory exists and writable\n";
        } else {
            $warnings[] = "{$name} directory is not writable: {$path}";
            echo "  ⚠️  {$name} - Directory not writable\n";
        }
    } else {
        $errors[] = "{$name} directory not found: {$path}";
        echo "  ❌ {$name} - Directory not found\n";
    }
}

// Test 5: Check required classes
echo "\n🏗️  Checking required classes...\n";

$classFiles = [
    'Logger' => __DIR__ . '/../src/Logger.php',
    'SoftOneGoClient' => __DIR__ . '/../src/SoftOneGoClient.php',
    'WooCommerceClient' => __DIR__ . '/../src/WooCommerceClient.php',
    'ProductSynchronizer' => __DIR__ . '/../src/ProductSynchronizer.php',
    'EmailNotifier' => __DIR__ . '/../src/EmailNotifier.php'
];

foreach ($classFiles as $className => $path) {
    if (file_exists($path)) {
        // Try to include the file
        try {
            require_once $path;
            if (class_exists($className)) {
                echo "  ✅ {$className} - Class exists and loadable\n";
            } else {
                $errors[] = "{$className} - File exists but class not found";
                echo "  ❌ {$className} - Class not found in file\n";
            }
        } catch (Exception $e) {
            $errors[] = "{$className} - Error loading: " . $e->getMessage();
            echo "  ❌ {$className} - Error loading\n";
        }
    } else {
        $errors[] = "{$className} - File not found: {$path}";
        echo "  ❌ {$className} - File not found\n";
    }
}

// Results summary
echo "\n📊 Test Results\n";
echo "===============\n";

if (empty($errors)) {
    echo "✅ All critical tests passed!\n";

    if (!empty($warnings)) {
        echo "\n⚠️  Warnings (" . count($warnings) . "):\n";
        foreach ($warnings as $warning) {
            echo "   • {$warning}\n";
        }
    }

    echo "\n🎉 Configuration appears to be valid!\n";
    echo "Next steps:\n";
    echo "  1. Test database connection: php tests/test_database.php\n";
    echo "  2. Test API connections: php tests/test_connections.php\n";
    echo "  3. Run a test sync: php sync.php --test --dry-run\n\n";

    exit(0);
} else {
    echo "❌ Configuration errors found (" . count($errors) . "):\n";
    foreach ($errors as $error) {
        echo "   • {$error}\n";
    }

    if (!empty($warnings)) {
        echo "\n⚠️  Warnings (" . count($warnings) . "):\n";
        foreach ($warnings as $warning) {
            echo "   • {$warning}\n";
        }
    }

    echo "\n🔧 Please fix the errors above before proceeding.\n\n";
    exit(1);
}