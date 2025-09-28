<?php

/**
 * Cron Job Installation Script
 * Αυτόματη εγκατάσταση cron job για το sync system
 *
 * Features:
 * - Automatic cron job installation
 * - 15-minute schedule setup
 * - Multiple execution prevention
 * - Error handling
 * - Backup existing crontab
 */

class CronInstaller {

    private $config;
    private $scriptPath;
    private $cronCommand;
    private $cronSchedule;

    public function __construct() {
        // Load configuration
        $this->config = require __DIR__ . '/config/app.php';
        $this->scriptPath = realpath(__DIR__ . '/sync.php');
        $this->cronSchedule = '*/15 * * * *'; // Every 15 minutes

        // Build cron command
        $this->cronCommand = $this->buildCronCommand();

        echo "🔧 SoftOne ATUM Sync - Cron Job Installer\n";
        echo "==========================================\n\n";
    }

    /**
     * Main installation method
     */
    public function install() {
        try {
            $this->validateEnvironment();
            $this->backupExistingCrontab();
            $this->installCronJob();
            $this->verifyCronJob();
            $this->showCompletionMessage();

        } catch (Exception $e) {
            echo "❌ Installation failed: " . $e->getMessage() . "\n";
            echo "Please install manually or contact system administrator.\n\n";
            exit(1);
        }
    }

    /**
     * Remove cron job
     */
    public function uninstall() {
        try {
            echo "🗑️  Removing SoftOne ATUM Sync cron job...\n\n";

            $this->removeCronJob();
            echo "✅ Cron job removed successfully!\n\n";

        } catch (Exception $e) {
            echo "❌ Uninstall failed: " . $e->getMessage() . "\n";
            exit(1);
        }
    }

    /**
     * Validate system environment
     */
    private function validateEnvironment() {
        echo "🔍 Validating environment...\n";

        // Check if script file exists
        if (!file_exists($this->scriptPath)) {
            throw new Exception("Sync script not found: {$this->scriptPath}");
        }

        // Check PHP CLI
        $phpPath = $this->findPhpPath();
        if (!$phpPath) {
            throw new Exception("PHP CLI not found in PATH");
        }

        // Check crontab command
        if (!$this->commandExists('crontab')) {
            throw new Exception("crontab command not found");
        }

        // Check script permissions
        if (!is_executable($this->scriptPath)) {
            echo "⚠️  Making sync script executable...\n";
            chmod($this->scriptPath, 0755);
        }

        // Check logs directory
        $logsDir = dirname($this->scriptPath) . '/logs';
        if (!is_dir($logsDir)) {
            echo "📁 Creating logs directory...\n";
            mkdir($logsDir, 0777, true);
        }

        if (!is_writable($logsDir)) {
            throw new Exception("Logs directory is not writable: {$logsDir}");
        }

        echo "✅ Environment validation passed\n\n";
    }

    /**
     * Backup existing crontab
     */
    private function backupExistingCrontab() {
        echo "💾 Backing up existing crontab...\n";

        $backupFile = dirname($this->scriptPath) . '/crontab_backup_' . date('Y-m-d_H-i-s') . '.txt';

        // Get current crontab
        $currentCrontab = shell_exec('crontab -l 2>/dev/null');

        if ($currentCrontab) {
            if (file_put_contents($backupFile, $currentCrontab)) {
                echo "✅ Crontab backed up to: {$backupFile}\n";
            } else {
                echo "⚠️  Warning: Could not create backup file\n";
            }
        } else {
            echo "ℹ️  No existing crontab found\n";
        }

        echo "\n";
    }

    /**
     * Install cron job
     */
    private function installCronJob() {
        echo "⚙️  Installing cron job...\n";

        // Get current crontab
        $currentCrontab = shell_exec('crontab -l 2>/dev/null') ?: '';

        // Check if our cron job already exists
        if (strpos($currentCrontab, 'SoftOne ATUM Sync') !== false) {
            echo "ℹ️  Cron job already exists, updating...\n";
            $this->removeCronJob();
            $currentCrontab = shell_exec('crontab -l 2>/dev/null') ?: '';
        }

        // Add our cron job
        $newCrontab = trim($currentCrontab) . "\n\n";
        $newCrontab .= "# SoftOne ATUM Sync - Auto-generated cron job\n";
        $newCrontab .= "# Runs every 15 minutes\n";
        $newCrontab .= "{$this->cronSchedule} {$this->cronCommand}\n";

        // Install new crontab
        $tempFile = tempnam(sys_get_temp_dir(), 'crontab_');
        file_put_contents($tempFile, $newCrontab);

        $result = shell_exec("crontab {$tempFile} 2>&1");
        unlink($tempFile);

        if ($result && strpos($result, 'error') !== false) {
            throw new Exception("Failed to install crontab: {$result}");
        }

        echo "✅ Cron job installed successfully\n";
        echo "📅 Schedule: Every 15 minutes\n";
        echo "🔧 Command: {$this->cronCommand}\n\n";
    }

    /**
     * Remove cron job
     */
    private function removeCronJob() {
        // Get current crontab
        $currentCrontab = shell_exec('crontab -l 2>/dev/null') ?: '';

        if (empty($currentCrontab)) {
            echo "ℹ️  No crontab entries found\n";
            return;
        }

        // Remove SoftOne ATUM Sync related lines
        $lines = explode("\n", $currentCrontab);
        $newLines = [];
        $skipNext = false;

        foreach ($lines as $line) {
            if (strpos($line, 'SoftOne ATUM Sync') !== false) {
                $skipNext = true;
                continue;
            }

            if ($skipNext && strpos($line, $this->scriptPath) !== false) {
                $skipNext = false;
                continue;
            }

            if (trim($line) !== '') {
                $newLines[] = $line;
            }
        }

        // Install updated crontab
        $newCrontab = implode("\n", $newLines) . "\n";
        $tempFile = tempnam(sys_get_temp_dir(), 'crontab_');
        file_put_contents($tempFile, $newCrontab);

        shell_exec("crontab {$tempFile} 2>&1");
        unlink($tempFile);
    }

    /**
     * Verify cron job installation
     */
    private function verifyCronJob() {
        echo "🔍 Verifying cron job installation...\n";

        $currentCrontab = shell_exec('crontab -l 2>/dev/null') ?: '';

        if (strpos($currentCrontab, $this->scriptPath) === false) {
            throw new Exception("Cron job verification failed - entry not found in crontab");
        }

        // Test script execution
        echo "🧪 Testing script execution...\n";
        $testCommand = $this->findPhpPath() . " {$this->scriptPath} --help";
        $testOutput = shell_exec($testCommand . ' 2>&1');

        if (strpos($testOutput, 'SoftOne Go to ATUM') === false) {
            throw new Exception("Script test failed - unexpected output");
        }

        echo "✅ Cron job verification passed\n\n";
    }

    /**
     * Build cron command
     */
    private function buildCronCommand() {
        $phpPath = $this->findPhpPath();
        $logFile = dirname($this->scriptPath) . '/logs/cron_' . date('Y-m') . '.log';

        return "{$phpPath} {$this->scriptPath} >> {$logFile} 2>&1";
    }

    /**
     * Find PHP executable path
     */
    private function findPhpPath() {
        // Common PHP paths
        $paths = [
            '/usr/bin/php',
            '/usr/local/bin/php',
            '/opt/php/bin/php',
            'php' // Will use PATH
        ];

        foreach ($paths as $path) {
            if ($path === 'php' || file_exists($path)) {
                $testCommand = "{$path} -v 2>/dev/null";
                $output = shell_exec($testCommand);

                if ($output && strpos($output, 'PHP') !== false) {
                    return $path;
                }
            }
        }

        return null;
    }

    /**
     * Check if command exists
     */
    private function commandExists($command) {
        $result = shell_exec("which {$command} 2>/dev/null");
        return !empty($result);
    }

    /**
     * Show completion message
     */
    private function showCompletionMessage() {
        echo "🎉 Installation completed successfully!\n\n";
        echo "📋 Summary:\n";
        echo "   • Cron job installed to run every 15 minutes\n";
        echo "   • Logs will be written to: logs/cron_YYYY-MM.log\n";
        echo "   • Lock file prevents overlapping executions\n";
        echo "   • You can manually run: php sync.php\n\n";

        echo "🔧 Management Commands:\n";
        echo "   • View crontab: crontab -l\n";
        echo "   • Edit crontab: crontab -e\n";
        echo "   • Remove cron job: php install_cron.php --uninstall\n\n";

        echo "📝 Next Steps:\n";
        echo "   1. Verify your .env configuration\n";
        echo "   2. Test connections with manual_sync.php\n";
        echo "   3. Monitor logs for any issues\n";
        echo "   4. Set up email notifications if needed\n\n";

        echo "⚠️  Important Notes:\n";
        echo "   • Make sure database credentials are correct\n";
        echo "   • Verify SoftOne Go and WooCommerce API access\n";
        echo "   • Check file permissions if sync fails\n\n";
    }

    /**
     * Show status of current cron jobs
     */
    public function status() {
        echo "📊 SoftOne ATUM Sync - Cron Status\n";
        echo "==================================\n\n";

        // Check current crontab
        $currentCrontab = shell_exec('crontab -l 2>/dev/null') ?: '';

        if (strpos($currentCrontab, 'SoftOne ATUM Sync') !== false) {
            echo "✅ Cron job is installed\n";

            // Extract our cron line
            $lines = explode("\n", $currentCrontab);
            foreach ($lines as $line) {
                if (strpos($line, $this->scriptPath) !== false) {
                    echo "📅 Schedule: {$line}\n";
                    break;
                }
            }
        } else {
            echo "❌ Cron job is not installed\n";
        }

        // Check recent log files
        $logsDir = dirname($this->scriptPath) . '/logs';
        if (is_dir($logsDir)) {
            $logFiles = glob($logsDir . '/cron_*.log');
            if (!empty($logFiles)) {
                $latestLog = max($logFiles);
                $logAge = time() - filemtime($latestLog);
                echo "📜 Latest log: " . basename($latestLog) . " (" . $this->formatTimeAgo($logAge) . " ago)\n";
            } else {
                echo "📜 No cron log files found\n";
            }
        }

        // Check lock file
        $lockFile = $this->config['lock_file'] ?? (dirname($this->scriptPath) . '/logs/sync.lock');
        if (file_exists($lockFile)) {
            $lockAge = time() - filemtime($lockFile);
            echo "🔒 Lock file exists (" . $this->formatTimeAgo($lockAge) . " ago)\n";
            if ($lockAge > 3600) {
                echo "   ⚠️  Warning: Lock file is old, process may be stuck\n";
            }
        } else {
            echo "🔓 No active lock file\n";
        }

        echo "\n";
    }

    /**
     * Format time ago string
     */
    private function formatTimeAgo($seconds) {
        if ($seconds < 60) return "{$seconds} seconds";
        if ($seconds < 3600) return round($seconds / 60) . " minutes";
        if ($seconds < 86400) return round($seconds / 3600) . " hours";
        return round($seconds / 86400) . " days";
    }
}

// Parse command line arguments
$options = getopt('', ['uninstall', 'status', 'help']);

if (isset($options['help'])) {
    echo "SoftOne ATUM Sync - Cron Job Installer\n";
    echo "======================================\n\n";
    echo "Usage: php install_cron.php [options]\n\n";
    echo "Options:\n";
    echo "  --uninstall    Remove cron job\n";
    echo "  --status       Show cron job status\n";
    echo "  --help         Show this help message\n\n";
    echo "Examples:\n";
    echo "  php install_cron.php           # Install cron job\n";
    echo "  php install_cron.php --status  # Check status\n";
    echo "  php install_cron.php --uninstall # Remove cron job\n\n";
    exit(0);
}

// Create installer instance
$installer = new CronInstaller();

// Execute requested action
if (isset($options['uninstall'])) {
    $installer->uninstall();
} elseif (isset($options['status'])) {
    $installer->status();
} else {
    $installer->install();
}