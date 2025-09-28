<?php

/**
 * Database Connection Test Script
 * Ελέγχει τη σύνδεση με τη βάση δεδομένων και τους πίνακες
 */

echo "🗄️  SoftOne ATUM Sync - Database Test\n";
echo "====================================\n\n";

// Load configurations
$dbConfig = require __DIR__ . '/../config/database.php';

// Load .env file if exists
$envFile = __DIR__ . '/../.env';
if (file_exists($envFile)) {
    $lines = file($envFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
    foreach ($lines as $line) {
        if (strpos($line, '=') !== false && strpos($line, '#') !== 0) {
            list($key, $value) = explode('=', $line, 2);
            $_ENV[trim($key)] = trim($value, '"\'');
        }
    }
}

$errors = [];
$warnings = [];

// Test 1: Database connection
echo "🔌 Testing database connection...\n";

try {
    $config = $dbConfig['default'];
    $dsn = "mysql:host={$config['host']};port={$config['port']};dbname={$config['database']};charset={$config['charset']}";

    $pdo = new PDO($dsn, $config['username'], $config['password'], $config['options']);

    echo "  ✅ Database connection successful\n";
    echo "  📊 Host: {$config['host']}:{$config['port']}\n";
    echo "  🗃️  Database: {$config['database']}\n";

    // Get MySQL version
    $versionStmt = $pdo->query('SELECT VERSION() as version');
    $version = $versionStmt->fetch()['version'];
    echo "  🐬 MySQL Version: {$version}\n";

} catch (PDOException $e) {
    $errors[] = "Database connection failed: " . $e->getMessage();
    echo "  ❌ Database connection failed\n";
    echo "     Error: " . $e->getMessage() . "\n";
}

if (!empty($errors)) {
    echo "\n❌ Cannot proceed without database connection.\n";
    echo "Please check your database configuration.\n\n";
    exit(1);
}

// Test 2: Check required tables
echo "\n📋 Checking required tables...\n";

$requiredTables = [
    'sync_logs' => 'Sync execution logs',
    'product_mappings' => 'Product ID mappings',
    'sync_statistics' => 'Daily sync statistics',
    'email_queue' => 'Email notification queue',
    'api_cache' => 'API response cache'
];

foreach ($requiredTables as $tableName => $description) {
    try {
        $stmt = $pdo->query("SHOW TABLES LIKE '{$tableName}'");
        $tableExists = $stmt->rowCount() > 0;

        if ($tableExists) {
            echo "  ✅ {$tableName} - Exists ({$description})\n";

            // Check table structure for critical tables
            if (in_array($tableName, ['sync_logs', 'product_mappings'])) {
                $stmt = $pdo->query("DESCRIBE {$tableName}");
                $columns = $stmt->fetchAll(PDO::FETCH_COLUMN);
                echo "     Columns: " . count($columns) . "\n";
            }
        } else {
            $errors[] = "Table '{$tableName}' not found";
            echo "  ❌ {$tableName} - Not found\n";
        }
    } catch (Exception $e) {
        $errors[] = "Error checking table '{$tableName}': " . $e->getMessage();
        echo "  ❌ {$tableName} - Error checking\n";
    }
}

// Test 3: Test database operations
if (empty($errors)) {
    echo "\n🧪 Testing database operations...\n";

    try {
        // Test INSERT
        $stmt = $pdo->prepare("
            INSERT INTO sync_logs (store_id, sync_type, start_time, status)
            VALUES ('test', 'test', NOW(), 'running')
        ");
        $stmt->execute();
        $testLogId = $pdo->lastInsertId();
        echo "  ✅ INSERT - Working (Test log ID: {$testLogId})\n";

        // Test UPDATE
        $stmt = $pdo->prepare("
            UPDATE sync_logs
            SET status = 'completed', end_time = NOW()
            WHERE id = ?
        ");
        $stmt->execute([$testLogId]);
        echo "  ✅ UPDATE - Working\n";

        // Test SELECT
        $stmt = $pdo->prepare("SELECT * FROM sync_logs WHERE id = ?");
        $stmt->execute([$testLogId]);
        $testLog = $stmt->fetch();

        if ($testLog) {
            echo "  ✅ SELECT - Working\n";
        } else {
            $warnings[] = "SELECT test returned no results";
        }

        // Test DELETE (cleanup)
        $stmt = $pdo->prepare("DELETE FROM sync_logs WHERE id = ?");
        $stmt->execute([$testLogId]);
        echo "  ✅ DELETE - Working (Test data cleaned up)\n";

    } catch (Exception $e) {
        $errors[] = "Database operations test failed: " . $e->getMessage();
        echo "  ❌ Database operations failed\n";
    }
}

// Test 4: Check indexes and performance
if (empty($errors)) {
    echo "\n⚡ Checking database performance...\n";

    try {
        // Check indexes on critical tables
        $indexQueries = [
            'sync_logs' => "SHOW INDEX FROM sync_logs",
            'product_mappings' => "SHOW INDEX FROM product_mappings"
        ];

        foreach ($indexQueries as $table => $query) {
            $stmt = $pdo->query($query);
            $indexes = $stmt->fetchAll();
            $indexCount = count(array_unique(array_column($indexes, 'Key_name')));
            echo "  📊 {$table} - {$indexCount} indexes\n";
        }

        // Test query performance
        $start = microtime(true);
        $stmt = $pdo->query("SELECT COUNT(*) as count FROM sync_logs");
        $duration = (microtime(true) - $start) * 1000;
        $count = $stmt->fetch()['count'];

        echo "  🏃 Query performance - {$duration}ms for {$count} sync logs\n";

        if ($duration > 1000) {
            $warnings[] = "Database queries are slow (>{$duration}ms)";
        }

    } catch (Exception $e) {
        $warnings[] = "Performance check failed: " . $e->getMessage();
    }
}

// Test 5: Check database size and limits
if (empty($errors)) {
    echo "\n💾 Checking database size and limits...\n";

    try {
        // Check database size
        $stmt = $pdo->prepare("
            SELECT
                ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) AS size_mb
            FROM information_schema.tables
            WHERE table_schema = ?
        ");
        $stmt->execute([$config['database']]);
        $sizeResult = $stmt->fetch();
        $sizeMB = $sizeResult['size_mb'] ?? 0;

        echo "  📏 Database size: {$sizeMB} MB\n";

        // Check connection limits
        $stmt = $pdo->query("SHOW VARIABLES LIKE 'max_connections'");
        $maxConnections = $stmt->fetch()['Value'] ?? 'unknown';
        echo "  🔗 Max connections: {$maxConnections}\n";

        // Check query cache
        $stmt = $pdo->query("SHOW VARIABLES LIKE 'query_cache_size'");
        $result = $stmt->fetch();
        if ($result && $result['Value'] > 0) {
            $cacheSize = round($result['Value'] / 1024 / 1024, 2);
            echo "  🗄️  Query cache: {$cacheSize} MB\n";
        } else {
            echo "  🗄️  Query cache: Disabled\n";
        }

    } catch (Exception $e) {
        $warnings[] = "Database info check failed: " . $e->getMessage();
    }
}

// Test 6: Check user privileges
echo "\n👤 Checking user privileges...\n";

try {
    $stmt = $pdo->query("SHOW GRANTS");
    $grants = $stmt->fetchAll(PDO::FETCH_COLUMN);

    $hasSelect = false;
    $hasInsert = false;
    $hasUpdate = false;
    $hasDelete = false;

    foreach ($grants as $grant) {
        if (strpos($grant, 'ALL PRIVILEGES') !== false) {
            $hasSelect = $hasInsert = $hasUpdate = $hasDelete = true;
            break;
        }
        if (strpos($grant, 'SELECT') !== false) $hasSelect = true;
        if (strpos($grant, 'INSERT') !== false) $hasInsert = true;
        if (strpos($grant, 'UPDATE') !== false) $hasUpdate = true;
        if (strpos($grant, 'DELETE') !== false) $hasDelete = true;
    }

    $privileges = [];
    if ($hasSelect) $privileges[] = 'SELECT';
    if ($hasInsert) $privileges[] = 'INSERT';
    if ($hasUpdate) $privileges[] = 'UPDATE';
    if ($hasDelete) $privileges[] = 'DELETE';

    echo "  🔑 User privileges: " . implode(', ', $privileges) . "\n";

    if (!($hasSelect && $hasInsert && $hasUpdate && $hasDelete)) {
        $warnings[] = "User may not have all required privileges";
    }

} catch (Exception $e) {
    $warnings[] = "Could not check user privileges: " . $e->getMessage();
}

// Results summary
echo "\n📊 Test Results\n";
echo "===============\n";

if (empty($errors)) {
    echo "✅ All database tests passed!\n";

    if (!empty($warnings)) {
        echo "\n⚠️  Warnings (" . count($warnings) . "):\n";
        foreach ($warnings as $warning) {
            echo "   • {$warning}\n";
        }
    }

    echo "\n🎉 Database is ready for synchronization!\n";
    echo "Next steps:\n";
    echo "  1. Test API connections: php tests/test_connections.php\n";
    echo "  2. Run configuration test: php tests/test_config.php\n";
    echo "  3. Try a test sync: php sync.php --test --dry-run\n\n";

    exit(0);
} else {
    echo "❌ Database errors found (" . count($errors) . "):\n";
    foreach ($errors as $error) {
        echo "   • {$error}\n";
    }

    if (!empty($warnings)) {
        echo "\n⚠️  Warnings (" . count($warnings) . "):\n";
        foreach ($warnings as $warning) {
            echo "   • {$warning}\n";
        }
    }

    echo "\n🔧 Please fix the errors above before proceeding.\n";
    echo "💡 Hint: Run the database schema script if tables are missing:\n";
    echo "   mysql -u username -p database_name < database/sync_logs.sql\n\n";

    exit(1);
}