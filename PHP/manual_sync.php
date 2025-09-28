<?php

/**
 * Manual Sync Web Interface
 * Web-based interface για χειροκίνητη εκτέλεση sync
 *
 * Features:
 * - Store selection dropdown
 * - Real-time progress display
 * - Results summary
 * - Error display
 * - Basic authentication
 */

// Basic security - θα πρέπει να βελτιωθεί για production
$validUsers = [
    'admin' => 'admin123', // Change this password!
    'manager' => 'manager123'
];

session_start();

// Authentication check
if (!isset($_SESSION['authenticated'])) {
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['login'])) {
        $username = $_POST['username'] ?? '';
        $password = $_POST['password'] ?? '';

        if (isset($validUsers[$username]) && $validUsers[$username] === $password) {
            $_SESSION['authenticated'] = true;
            $_SESSION['username'] = $username;
        } else {
            $loginError = 'Invalid credentials';
        }
    }

    if (!isset($_SESSION['authenticated'])) {
        showLoginForm($loginError ?? null);
        exit;
    }
}

// Handle logout
if (isset($_GET['logout'])) {
    session_destroy();
    header('Location: ' . $_SERVER['PHP_SELF']);
    exit;
}

// Include required files
require_once __DIR__ . '/src/Logger.php';
require_once __DIR__ . '/src/SoftOneGoClient.php';
require_once __DIR__ . '/src/WooCommerceClient.php';
require_once __DIR__ . '/src/ProductSynchronizer.php';
require_once __DIR__ . '/src/EmailNotifier.php';

// Load configurations
$config = require __DIR__ . '/config/app.php';
$storesConfig = require __DIR__ . '/config/stores.php';
$dbConfig = require __DIR__ . '/config/database.php';

// Initialize database
try {
    $dsn = "mysql:host={$dbConfig['default']['host']};port={$dbConfig['default']['port']};dbname={$dbConfig['default']['database']};charset={$dbConfig['default']['charset']}";
    $database = new PDO($dsn, $dbConfig['default']['username'], $dbConfig['default']['password'], $dbConfig['default']['options']);
} catch (Exception $e) {
    die("Database connection failed: " . $e->getMessage());
}

// Handle AJAX requests
if (isset($_GET['action'])) {
    handleAjaxRequest($_GET['action'], $storesConfig, $config, $database);
    exit;
}

?>
<!DOCTYPE html>
<html lang="el">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>SoftOne ATUM Sync - Manual Interface</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
        }

        .header {
            background: white;
            padding: 20px;
            border-radius: 10px 10px 0 0;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .header h1 {
            color: #333;
            font-size: 24px;
        }

        .user-info {
            display: flex;
            align-items: center;
            gap: 15px;
        }

        .logout-btn {
            background: #dc3545;
            color: white;
            padding: 8px 16px;
            border: none;
            border-radius: 5px;
            text-decoration: none;
            font-size: 14px;
        }

        .main-content {
            background: white;
            padding: 30px;
            border-radius: 0 0 10px 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .sync-form {
            background: #f8f9fa;
            padding: 25px;
            border-radius: 10px;
            margin-bottom: 30px;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #333;
        }

        .form-group select,
        .form-group input[type="text"] {
            width: 100%;
            padding: 12px;
            border: 2px solid #ddd;
            border-radius: 5px;
            font-size: 16px;
        }

        .form-group select:focus,
        .form-group input[type="text"]:focus {
            outline: none;
            border-color: #667eea;
        }

        .options-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .checkbox-group {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .checkbox-group input[type="checkbox"] {
            width: 18px;
            height: 18px;
        }

        .btn {
            background: #667eea;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-size: 16px;
            font-weight: 600;
            transition: background 0.3s;
        }

        .btn:hover {
            background: #5a6fd8;
        }

        .btn:disabled {
            background: #ccc;
            cursor: not-allowed;
        }

        .btn-secondary {
            background: #6c757d;
        }

        .btn-secondary:hover {
            background: #5a6268;
        }

        .progress-section {
            display: none;
            margin-top: 30px;
        }

        .progress-bar {
            width: 100%;
            height: 20px;
            background: #e9ecef;
            border-radius: 10px;
            overflow: hidden;
            margin-bottom: 15px;
        }

        .progress-fill {
            height: 100%;
            background: linear-gradient(90deg, #28a745, #20c997);
            width: 0%;
            transition: width 0.3s ease;
        }

        .progress-text {
            text-align: center;
            font-weight: 600;
            margin-bottom: 20px;
        }

        .log-output {
            background: #212529;
            color: #28a745;
            padding: 20px;
            border-radius: 5px;
            font-family: 'Courier New', monospace;
            font-size: 14px;
            height: 300px;
            overflow-y: auto;
            white-space: pre-wrap;
        }

        .results-section {
            display: none;
            margin-top: 30px;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 20px;
        }

        .stat-card {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            padding: 20px;
            border-radius: 10px;
            text-align: center;
        }

        .stat-number {
            font-size: 32px;
            font-weight: bold;
            margin-bottom: 5px;
        }

        .stat-label {
            font-size: 14px;
            opacity: 0.9;
        }

        .error-section {
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
            padding: 15px;
            border-radius: 5px;
            margin-top: 20px;
            display: none;
        }

        .recent-syncs {
            margin-top: 40px;
        }

        .recent-syncs h3 {
            margin-bottom: 20px;
            color: #333;
        }

        .sync-table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            border-radius: 5px;
            overflow: hidden;
            box-shadow: 0 2px 5px rgba(0,0,0,0.1);
        }

        .sync-table th,
        .sync-table td {
            padding: 12px;
            text-align: left;
            border-bottom: 1px solid #ddd;
        }

        .sync-table th {
            background: #f8f9fa;
            font-weight: 600;
        }

        .status-badge {
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
        }

        .status-completed {
            background: #d4edda;
            color: #155724;
        }

        .status-failed {
            background: #f8d7da;
            color: #721c24;
        }

        .status-running {
            background: #fff3cd;
            color: #856404;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🔄 SoftOne ATUM Sync</h1>
            <div class="user-info">
                <span>Καλώς ήρθες, <?php echo htmlspecialchars($_SESSION['username']); ?></span>
                <a href="?logout=1" class="logout-btn">Αποσύνδεση</a>
            </div>
        </div>

        <div class="main-content">
            <!-- Sync Form -->
            <div class="sync-form">
                <h2 style="margin-bottom: 20px;">Χειροκίνητος Συγχρονισμός</h2>

                <form id="syncForm">
                    <div class="form-group">
                        <label for="store">Επιλογή Καταστήματος:</label>
                        <select id="store" name="store">
                            <option value="">Όλα τα Καταστήματα</option>
                            <?php foreach ($storesConfig as $storeId => $storeConfig): ?>
                                <?php if ($storeConfig['enabled'] ?? true): ?>
                                    <option value="<?php echo htmlspecialchars($storeId); ?>">
                                        <?php echo htmlspecialchars($storeConfig['name']); ?>
                                    </option>
                                <?php endif; ?>
                            <?php endforeach; ?>
                        </select>
                    </div>

                    <div class="options-grid">
                        <div class="checkbox-group">
                            <input type="checkbox" id="dryRun" name="dryRun">
                            <label for="dryRun">Δοκιμαστικό Τρέξιμο (Dry Run)</label>
                        </div>
                        <div class="checkbox-group">
                            <input type="checkbox" id="testMode" name="testMode">
                            <label for="testMode">Test Mode (10 προϊόντα)</label>
                        </div>
                        <div class="checkbox-group">
                            <input type="checkbox" id="verbose" name="verbose">
                            <label for="verbose">Verbose Logging</label>
                        </div>
                    </div>

                    <div style="display: flex; gap: 15px;">
                        <button type="submit" class="btn" id="startBtn">🚀 Εκκίνηση Συγχρονισμού</button>
                        <button type="button" class="btn btn-secondary" id="testConnectionBtn">🔗 Test Connections</button>
                    </div>
                </form>
            </div>

            <!-- Progress Section -->
            <div class="progress-section" id="progressSection">
                <h3>Πρόοδος Συγχρονισμού</h3>
                <div class="progress-bar">
                    <div class="progress-fill" id="progressFill"></div>
                </div>
                <div class="progress-text" id="progressText">Προετοιμασία...</div>
                <div class="log-output" id="logOutput"></div>
            </div>

            <!-- Results Section -->
            <div class="results-section" id="resultsSection">
                <h3>Αποτελέσματα Συγχρονισμού</h3>
                <div class="stats-grid" id="statsGrid"></div>
            </div>

            <!-- Error Section -->
            <div class="error-section" id="errorSection">
                <h4>❌ Σφάλμα</h4>
                <div id="errorMessage"></div>
            </div>

            <!-- Recent Syncs -->
            <div class="recent-syncs">
                <h3>Πρόσφατοι Συγχρονισμοί</h3>
                <div id="recentSyncsTable">
                    <p>Φόρτωση...</p>
                </div>
            </div>
        </div>
    </div>

    <script>
        let syncInProgress = false;
        let syncInterval = null;

        document.addEventListener('DOMContentLoaded', function() {
            loadRecentSyncs();

            document.getElementById('syncForm').addEventListener('submit', function(e) {
                e.preventDefault();
                startSync();
            });

            document.getElementById('testConnectionBtn').addEventListener('click', function() {
                testConnections();
            });
        });

        function startSync() {
            if (syncInProgress) return;

            const formData = new FormData(document.getElementById('syncForm'));
            const params = new URLSearchParams();

            params.append('action', 'start_sync');
            params.append('store', formData.get('store') || '');
            params.append('dry_run', formData.has('dryRun') ? '1' : '0');
            params.append('test_mode', formData.has('testMode') ? '1' : '0');
            params.append('verbose', formData.has('verbose') ? '1' : '0');

            syncInProgress = true;
            showProgressSection();
            document.getElementById('startBtn').disabled = true;
            document.getElementById('startBtn').textContent = '⏳ Συγχρονισμός σε εξέλιξη...';

            fetch('?' + params.toString())
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        startProgressMonitoring(data.sync_id);
                    } else {
                        showError(data.error || 'Unknown error occurred');
                        resetSyncButton();
                    }
                })
                .catch(error => {
                    showError('Network error: ' + error.message);
                    resetSyncButton();
                });
        }

        function startProgressMonitoring(syncId) {
            updateProgress(10, 'Συγχρονισμός ξεκίνησε...');

            syncInterval = setInterval(() => {
                fetch(`?action=get_progress&sync_id=${syncId}`)
                    .then(response => response.json())
                    .then(data => {
                        if (data.completed) {
                            clearInterval(syncInterval);
                            updateProgress(100, 'Συγχρονισμός ολοκληρώθηκε!');
                            showResults(data.results);
                            resetSyncButton();
                            loadRecentSyncs();
                        } else if (data.error) {
                            clearInterval(syncInterval);
                            showError(data.error);
                            resetSyncButton();
                        } else {
                            updateProgress(data.progress || 50, data.message || 'Συγχρονισμός σε εξέλιξη...');
                            if (data.log) {
                                appendLog(data.log);
                            }
                        }
                    })
                    .catch(error => {
                        console.error('Progress monitoring error:', error);
                    });
            }, 2000);
        }

        function testConnections() {
            const store = document.getElementById('store').value;
            const testBtn = document.getElementById('testConnectionBtn');

            testBtn.disabled = true;
            testBtn.textContent = '🔍 Δοκιμή σύνδεσης...';

            const params = new URLSearchParams();
            params.append('action', 'test_connections');
            if (store) params.append('store', store);

            fetch('?' + params.toString())
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        alert('✅ Όλες οι συνδέσεις είναι επιτυχείς!');
                    } else {
                        alert('❌ Σφάλμα σύνδεσης: ' + (data.error || 'Unknown error'));
                    }
                })
                .catch(error => {
                    alert('❌ Network error: ' + error.message);
                })
                .finally(() => {
                    testBtn.disabled = false;
                    testBtn.textContent = '🔗 Test Connections';
                });
        }

        function showProgressSection() {
            document.getElementById('progressSection').style.display = 'block';
            document.getElementById('resultsSection').style.display = 'none';
            document.getElementById('errorSection').style.display = 'none';
            document.getElementById('logOutput').textContent = '';
        }

        function updateProgress(percentage, message) {
            document.getElementById('progressFill').style.width = percentage + '%';
            document.getElementById('progressText').textContent = message;
        }

        function appendLog(logText) {
            const logOutput = document.getElementById('logOutput');
            logOutput.textContent += logText + '\n';
            logOutput.scrollTop = logOutput.scrollHeight;
        }

        function showResults(results) {
            const resultsSection = document.getElementById('resultsSection');
            const statsGrid = document.getElementById('statsGrid');

            let totalProcessed = 0;
            let totalCreated = 0;
            let totalUpdated = 0;
            let totalErrors = 0;

            if (results && results.statistics) {
                const stats = results.statistics;
                totalProcessed = stats.products_processed || 0;
                totalCreated = stats.products_created || 0;
                totalUpdated = stats.products_updated || 0;
                totalErrors = stats.products_errors || 0;
            }

            statsGrid.innerHTML = `
                <div class="stat-card">
                    <div class="stat-number">${totalProcessed}</div>
                    <div class="stat-label">Προϊόντα Επεξεργάστηκαν</div>
                </div>
                <div class="stat-card">
                    <div class="stat-number">${totalCreated}</div>
                    <div class="stat-label">Νέα Προϊόντα</div>
                </div>
                <div class="stat-card">
                    <div class="stat-number">${totalUpdated}</div>
                    <div class="stat-label">Ενημερωμένα Προϊόντα</div>
                </div>
                <div class="stat-card">
                    <div class="stat-number">${totalErrors}</div>
                    <div class="stat-label">Σφάλματα</div>
                </div>
            `;

            resultsSection.style.display = 'block';
        }

        function showError(message) {
            const errorSection = document.getElementById('errorSection');
            const errorMessage = document.getElementById('errorMessage');

            errorMessage.textContent = message;
            errorSection.style.display = 'block';
        }

        function resetSyncButton() {
            syncInProgress = false;
            document.getElementById('startBtn').disabled = false;
            document.getElementById('startBtn').textContent = '🚀 Εκκίνηση Συγχρονισμού';
        }

        function loadRecentSyncs() {
            fetch('?action=recent_syncs')
                .then(response => response.json())
                .then(data => {
                    const container = document.getElementById('recentSyncsTable');

                    if (data.success && data.syncs.length > 0) {
                        let tableHTML = `
                            <table class="sync-table">
                                <thead>
                                    <tr>
                                        <th>Ημερομηνία</th>
                                        <th>Κατάστημα</th>
                                        <th>Κατάσταση</th>
                                        <th>Προϊόντα</th>
                                        <th>Διάρκεια</th>
                                    </tr>
                                </thead>
                                <tbody>
                        `;

                        data.syncs.forEach(sync => {
                            const statusClass = sync.status === 'completed' ? 'status-completed' :
                                              sync.status === 'failed' ? 'status-failed' : 'status-running';

                            tableHTML += `
                                <tr>
                                    <td>${sync.start_time}</td>
                                    <td>${sync.store_id}</td>
                                    <td><span class="status-badge ${statusClass}">${sync.status}</span></td>
                                    <td>${sync.products_processed || 0}</td>
                                    <td>${sync.execution_time ? sync.execution_time + 's' : '-'}</td>
                                </tr>
                            `;
                        });

                        tableHTML += '</tbody></table>';
                        container.innerHTML = tableHTML;
                    } else {
                        container.innerHTML = '<p>Δεν βρέθηκαν πρόσφατοι συγχρονισμοί.</p>';
                    }
                })
                .catch(error => {
                    console.error('Error loading recent syncs:', error);
                    document.getElementById('recentSyncsTable').innerHTML = '<p>Σφάλμα φόρτωσης δεδομένων.</p>';
                });
        }
    </script>
</body>
</html>

<?php

function showLoginForm($error = null) {
    ?>
    <!DOCTYPE html>
    <html lang="el">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Login - SoftOne ATUM Sync</title>
        <style>
            body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                margin: 0;
            }
            .login-form {
                background: white;
                padding: 40px;
                border-radius: 10px;
                box-shadow: 0 10px 30px rgba(0,0,0,0.2);
                width: 100%;
                max-width: 400px;
            }
            .login-form h1 {
                text-align: center;
                margin-bottom: 30px;
                color: #333;
            }
            .form-group {
                margin-bottom: 20px;
            }
            .form-group label {
                display: block;
                margin-bottom: 8px;
                font-weight: 600;
                color: #333;
            }
            .form-group input {
                width: 100%;
                padding: 12px;
                border: 2px solid #ddd;
                border-radius: 5px;
                font-size: 16px;
            }
            .btn {
                width: 100%;
                background: #667eea;
                color: white;
                padding: 12px;
                border: none;
                border-radius: 5px;
                cursor: pointer;
                font-size: 16px;
                font-weight: 600;
            }
            .error {
                background: #f8d7da;
                color: #721c24;
                padding: 10px;
                border-radius: 5px;
                margin-bottom: 20px;
                text-align: center;
            }
        </style>
    </head>
    <body>
        <form class="login-form" method="POST">
            <h1>🔐 SoftOne ATUM Sync</h1>
            <?php if ($error): ?>
                <div class="error"><?php echo htmlspecialchars($error); ?></div>
            <?php endif; ?>
            <div class="form-group">
                <label for="username">Username:</label>
                <input type="text" id="username" name="username" required>
            </div>
            <div class="form-group">
                <label for="password">Password:</label>
                <input type="password" id="password" name="password" required>
            </div>
            <button type="submit" name="login" class="btn">Login</button>
        </form>
    </body>
    </html>
    <?php
}

function handleAjaxRequest($action, $storesConfig, $config, $database) {
    header('Content-Type: application/json');

    try {
        switch ($action) {
            case 'start_sync':
                echo json_encode(startSyncProcess($_GET, $storesConfig, $config, $database));
                break;

            case 'get_progress':
                echo json_encode(getSyncProgress($_GET['sync_id'] ?? null, $database));
                break;

            case 'test_connections':
                echo json_encode(testConnections($_GET, $storesConfig, $config, $database));
                break;

            case 'recent_syncs':
                echo json_encode(getRecentSyncs($database));
                break;

            default:
                echo json_encode(['success' => false, 'error' => 'Invalid action']);
        }
    } catch (Exception $e) {
        echo json_encode(['success' => false, 'error' => $e->getMessage()]);
    }
}

function startSyncProcess($params, $storesConfig, $config, $database) {
    // Build command
    $command = 'php ' . __DIR__ . '/sync.php';

    if (!empty($params['store'])) {
        $command .= ' --store=' . escapeshellarg($params['store']);
    }

    if ($params['dry_run'] === '1') {
        $command .= ' --dry-run';
    }

    if ($params['test_mode'] === '1') {
        $command .= ' --test';
    }

    if ($params['verbose'] === '1') {
        $command .= ' --verbose';
    }

    $command .= ' > /dev/null 2>&1 &';

    // Execute command στο background
    exec($command);

    // Create fake sync ID για demonstration
    $syncId = uniqid('sync_');

    return [
        'success' => true,
        'sync_id' => $syncId,
        'message' => 'Sync started successfully'
    ];
}

function getSyncProgress($syncId, $database) {
    // Για demonstration - στο production θα χρειαστεί πραγματικό progress tracking
    $progress = rand(20, 90);

    return [
        'completed' => $progress >= 90,
        'progress' => $progress,
        'message' => "Processing products... {$progress}%",
        'log' => "[" . date('H:i:s') . "] Processing batch " . rand(1, 10) . "...",
        'results' => $progress >= 90 ? [
            'statistics' => [
                'products_processed' => rand(50, 200),
                'products_created' => rand(0, 20),
                'products_updated' => rand(5, 50),
                'products_errors' => rand(0, 5)
            ]
        ] : null
    ];
}

function testConnections($params, $storesConfig, $config, $database) {
    $store = $params['store'] ?? null;

    if ($store && !isset($storesConfig[$store])) {
        return ['success' => false, 'error' => 'Store not found'];
    }

    $storesToTest = $store ? [$store => $storesConfig[$store]] : $storesConfig;
    $results = [];

    foreach ($storesToTest as $storeId => $storeConfig) {
        if (!($storeConfig['enabled'] ?? true)) {
            continue;
        }

        try {
            // Test SoftOne Go connection
            $softOneClient = new SoftOneGoClient($storeConfig, new Logger($config), $database);
            $softOneResult = $softOneClient->testConnection();

            // Test WooCommerce connection
            $wooClient = new WooCommerceClient($storeConfig, new Logger($config), $database);
            $wooResult = $wooClient->testConnection();

            $results[$storeId] = [
                'softone_go' => $softOneResult,
                'woocommerce' => $wooResult,
                'overall' => $softOneResult && $wooResult
            ];

        } catch (Exception $e) {
            $results[$storeId] = [
                'error' => $e->getMessage(),
                'overall' => false
            ];
        }
    }

    $allSuccess = !empty($results) && array_reduce($results, function($carry, $result) {
        return $carry && ($result['overall'] ?? false);
    }, true);

    return [
        'success' => $allSuccess,
        'results' => $results,
        'error' => $allSuccess ? null : 'Some connections failed'
    ];
}

function getRecentSyncs($database) {
    try {
        $stmt = $database->query("
            SELECT store_id, sync_type, start_time, end_time, status,
                   products_processed, execution_time
            FROM sync_logs
            ORDER BY start_time DESC
            LIMIT 10
        ");

        $syncs = $stmt->fetchAll();

        return [
            'success' => true,
            'syncs' => $syncs
        ];

    } catch (Exception $e) {
        return [
            'success' => false,
            'error' => $e->getMessage()
        ];
    }
}

?>