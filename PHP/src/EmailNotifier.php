<?php

/**
 * Email Notifier
 * Διαχείριση email notifications για sync events
 *
 * Features:
 * - SMTP configuration και sending
 * - HTML email templates
 * - New products notifications
 * - Error notifications
 * - Daily summary reports
 * - Email queue system με retry logic
 * - Template system για reusable emails
 */

class EmailNotifier {

    private $config;
    private $logger;
    private $database;
    private $smtpConfig;

    /**
     * Constructor
     *
     * @param array $config App configuration
     * @param Logger $logger Logger instance
     * @param PDO $database Database connection
     */
    public function __construct($config, $logger, $database = null) {
        $this->config = $config;
        $this->logger = $logger;
        $this->database = $database;

        // SMTP configuration από app config
        $this->smtpConfig = $this->config['smtp'] ?? [
            'host' => 'localhost',
            'port' => 587,
            'username' => '',
            'password' => '',
            'encryption' => 'tls',
            'from_email' => 'noreply@example.com',
            'from_name' => 'SoftOne ATUM Sync',
            'timeout' => 30,
            'auth' => true
        ];

        $this->logger->info('EmailNotifier initialized', [
            'smtp_host' => $this->smtpConfig['host'],
            'smtp_port' => $this->smtpConfig['port'],
            'smtp_encryption' => $this->smtpConfig['encryption'],
            'from_email' => $this->smtpConfig['from_email']
        ]);
    }

    /**
     * Send new products notification
     *
     * @param array $newProducts List of new products
     * @param array $syncStats Sync statistics
     * @param int $syncLogId Sync log ID
     */
    public function sendNewProductsNotification($newProducts, $syncStats, $syncLogId = null) {
        if (!$this->config['email_on_new_products'] || empty($newProducts)) {
            return;
        }

        $this->logger->info('Sending new products notification', [
            'new_products_count' => count($newProducts),
            'sync_log_id' => $syncLogId
        ]);

        $subject = "Νέα Προϊόντα - " . ($this->config['app_name'] ?? 'SoftOne ATUM Sync');
        $body = $this->buildNewProductsEmail($newProducts, $syncStats);

        $this->queueEmail('new_products', $subject, $body, $syncLogId);
    }

    /**
     * Send error notification
     *
     * @param string $errorMessage Error message
     * @param array $context Error context
     * @param int $syncLogId Sync log ID
     */
    public function sendErrorNotification($errorMessage, $context = [], $syncLogId = null) {
        if (!$this->config['email_on_error']) {
            return;
        }

        $this->logger->info('Sending error notification', [
            'error_message' => substr($errorMessage, 0, 100),
            'sync_log_id' => $syncLogId
        ]);

        $subject = "Σφάλμα Συγχρονισμού - " . ($this->config['app_name'] ?? 'SoftOne ATUM Sync');
        $body = $this->buildErrorEmail($errorMessage, $context);

        $this->queueEmail('error_notification', $subject, $body, $syncLogId);
    }

    /**
     * Send daily summary report
     *
     * @param array $summaryData Daily summary data
     */
    public function sendDailySummary($summaryData) {
        if (!$this->config['email_daily_summary']) {
            return;
        }

        $this->logger->info('Sending daily summary', [
            'summary_date' => $summaryData['date'] ?? date('Y-m-d')
        ]);

        $subject = "Ημερήσια Αναφορά Συγχρονισμού - " . ($summaryData['date'] ?? date('Y-m-d'));
        $body = $this->buildDailySummaryEmail($summaryData);

        $this->queueEmail('daily_summary', $subject, $body);
    }

    /**
     * Send test email
     *
     * @return bool Success status
     */
    public function sendTestEmail() {
        $this->logger->info('Sending test email');

        $subject = "Test Email - " . ($this->config['app_name'] ?? 'SoftOne ATUM Sync');
        $body = $this->buildTestEmail();

        return $this->sendEmailDirect('test@example.com', $subject, $body);
    }

    /**
     * Queue email για sending
     *
     * @param string $emailType Type of email
     * @param string $subject Email subject
     * @param string $body Email body (HTML)
     * @param int $syncLogId Associated sync log ID
     */
    private function queueEmail($emailType, $subject, $body, $syncLogId = null) {
        if (!$this->database) {
            $this->logger->warning('No database connection, cannot queue email');
            return;
        }

        $recipients = $this->config['email_recipients'] ?? ['admin@example.com'];

        try {
            $stmt = $this->database->prepare("
                INSERT INTO email_queue (
                    sync_log_id, email_type, recipient, subject, body, scheduled_at
                ) VALUES (?, ?, ?, ?, ?, NOW())
            ");

            foreach ($recipients as $recipient) {
                $stmt->execute([$syncLogId, $emailType, $recipient, $subject, $body]);

                $this->logger->debug('Email queued', [
                    'type' => $emailType,
                    'recipient' => $recipient,
                    'subject' => $subject
                ]);
            }

        } catch (Exception $e) {
            $this->logger->error('Failed to queue email', [
                'error' => $e->getMessage(),
                'email_type' => $emailType
            ]);
        }
    }

    /**
     * Process email queue και send pending emails
     *
     * @param int $limit Maximum emails to send στο run
     * @return int Number of emails sent
     */
    public function processEmailQueue($limit = 10) {
        if (!$this->database) {
            return 0;
        }

        $this->logger->info('Processing email queue', ['limit' => $limit]);

        try {
            // Get pending emails
            $stmt = $this->database->prepare("
                SELECT id, recipient, subject, body, email_type, attempts
                FROM email_queue
                WHERE status = 'pending' AND attempts < max_attempts
                ORDER BY scheduled_at ASC
                LIMIT ?
            ");
            $stmt->execute([$limit]);
            $emails = $stmt->fetchAll();

            $sentCount = 0;

            foreach ($emails as $email) {
                try {
                    $success = $this->sendEmailDirect($email['recipient'], $email['subject'], $email['body']);

                    if ($success) {
                        $this->markEmailSent($email['id']);
                        $sentCount++;

                        $this->logger->info('Email sent successfully', [
                            'email_id' => $email['id'],
                            'recipient' => $email['recipient'],
                            'type' => $email['email_type']
                        ]);
                    } else {
                        $this->markEmailFailed($email['id'], 'SMTP send failed');
                    }

                } catch (Exception $e) {
                    $this->markEmailFailed($email['id'], $e->getMessage());

                    $this->logger->error('Email send failed', [
                        'email_id' => $email['id'],
                        'error' => $e->getMessage()
                    ]);
                }

                // Rate limiting - wait between emails
                sleep(1);
            }

            $this->logger->info('Email queue processing completed', [
                'emails_processed' => count($emails),
                'emails_sent' => $sentCount
            ]);

            return $sentCount;

        } catch (Exception $e) {
            $this->logger->error('Email queue processing failed', [
                'error' => $e->getMessage()
            ]);
            return 0;
        }
    }

    /**
     * Send email directly μέσω SMTP
     *
     * @param string $recipient
     * @param string $subject
     * @param string $body
     * @return bool Success status
     */
    private function sendEmailDirect($recipient, $subject, $body) {
        try {
            // For production, consider using PHPMailer or SwiftMailer for proper SMTP support
            // This is a basic implementation using PHP's mail() function

            // Build email headers
            $headers = [
                'MIME-Version: 1.0',
                'Content-Type: text/html; charset=UTF-8',
                'From: ' . $this->smtpConfig['from_name'] . ' <' . $this->smtpConfig['from_email'] . '>',
                'Reply-To: ' . $this->smtpConfig['from_email'],
                'X-Mailer: SoftOne-ATUM-Sync/1.0',
                'X-Priority: 3',
                'Message-ID: <' . uniqid() . '@' . $_SERVER['HTTP_HOST'] . '>'
            ];

            // Add additional headers for better deliverability
            if (!empty($this->smtpConfig['username'])) {
                $headers[] = 'Return-Path: ' . $this->smtpConfig['from_email'];
            }

            // Send email using PHP's mail() function
            // Note: For production SMTP with authentication, consider:
            // - PHPMailer: https://github.com/PHPMailer/PHPMailer
            // - SwiftMailer: https://swiftmailer.symfony.com/
            // - Symfony Mailer: https://symfony.com/doc/current/mailer.html

            $success = mail($recipient, $subject, $body, implode("\r\n", $headers));

            if (!$success) {
                throw new Exception('mail() function failed - check server mail configuration');
            }

            return true;

        } catch (Exception $e) {
            $this->logger->error('Direct email send failed', [
                'recipient' => $recipient,
                'smtp_host' => $this->smtpConfig['host'],
                'error' => $e->getMessage()
            ]);
            return false;
        }
    }

    /**
     * Alternative SMTP implementation using PHPMailer (for reference)
     * Uncomment and install PHPMailer for production SMTP support
     */
    /*
    private function sendEmailDirectSMTP($recipient, $subject, $body) {
        require_once 'vendor/autoload.php';

        $mail = new PHPMailer\PHPMailer\PHPMailer(true);

        try {
            // SMTP configuration
            $mail->isSMTP();
            $mail->Host = $this->smtpConfig['host'];
            $mail->SMTPAuth = $this->smtpConfig['auth'];
            $mail->Username = $this->smtpConfig['username'];
            $mail->Password = $this->smtpConfig['password'];
            $mail->SMTPSecure = $this->smtpConfig['encryption'];
            $mail->Port = $this->smtpConfig['port'];
            $mail->Timeout = $this->smtpConfig['timeout'];

            // Email content
            $mail->setFrom($this->smtpConfig['from_email'], $this->smtpConfig['from_name']);
            $mail->addAddress($recipient);
            $mail->isHTML(true);
            $mail->CharSet = 'UTF-8';
            $mail->Subject = $subject;
            $mail->Body = $body;

            $mail->send();
            return true;

        } catch (Exception $e) {
            $this->logger->error('SMTP email send failed', [
                'recipient' => $recipient,
                'error' => $e->getMessage()
            ]);
            return false;
        }
    }
    */

    /**
     * Mark email as sent στο database
     *
     * @param int $emailId
     */
    private function markEmailSent($emailId) {
        if (!$this->database) {
            return;
        }

        try {
            $stmt = $this->database->prepare("
                UPDATE email_queue
                SET status = 'sent', sent_at = NOW()
                WHERE id = ?
            ");
            $stmt->execute([$emailId]);

        } catch (Exception $e) {
            $this->logger->warning('Failed to mark email as sent', [
                'email_id' => $emailId,
                'error' => $e->getMessage()
            ]);
        }
    }

    /**
     * Mark email as failed στο database
     *
     * @param int $emailId
     * @param string $errorMessage
     */
    private function markEmailFailed($emailId, $errorMessage) {
        if (!$this->database) {
            return;
        }

        try {
            $stmt = $this->database->prepare("
                UPDATE email_queue
                SET status = CASE
                    WHEN attempts + 1 >= max_attempts THEN 'failed'
                    ELSE 'pending'
                END,
                attempts = attempts + 1,
                error_message = ?,
                scheduled_at = CASE
                    WHEN attempts + 1 < max_attempts THEN DATE_ADD(NOW(), INTERVAL POW(2, attempts + 1) MINUTE)
                    ELSE scheduled_at
                END
                WHERE id = ?
            ");
            $stmt->execute([$errorMessage, $emailId]);

        } catch (Exception $e) {
            $this->logger->warning('Failed to mark email as failed', [
                'email_id' => $emailId,
                'error' => $e->getMessage()
            ]);
        }
    }

    /**
     * Build new products email HTML
     *
     * @param array $newProducts
     * @param array $syncStats
     * @return string HTML email body
     */
    private function buildNewProductsEmail($newProducts, $syncStats) {
        $productCount = count($newProducts);
        $date = date('d/m/Y H:i');

        $html = $this->getEmailTemplate();
        $html = str_replace('{{TITLE}}', "Νέα Προϊόντα ({$productCount})", $html);

        $content = "<h2>Νέα Προϊόντα Προστέθηκαν</h2>";
        $content .= "<p>Κατά τη διάρκεια του συγχρονισμού στις {$date}, προστέθηκαν {$productCount} νέα προϊόντα:</p>";

        $content .= "<table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>";
        $content .= "<tr style='background-color: #f5f5f5;'>";
        $content .= "<th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>SKU</th>";
        $content .= "<th style='border: 1px solid #ddd; padding: 8px; text-align: left;'>Όνομα</th>";
        $content .= "<th style='border: 1px solid #ddd; padding: 8px; text-align: right;'>Ποσότητα</th>";
        $content .= "</tr>";

        foreach ($newProducts as $product) {
            $content .= "<tr>";
            $content .= "<td style='border: 1px solid #ddd; padding: 8px;'>" . htmlspecialchars($product['sku']) . "</td>";
            $content .= "<td style='border: 1px solid #ddd; padding: 8px;'>" . htmlspecialchars($product['name']) . "</td>";
            $content .= "<td style='border: 1px solid #ddd; padding: 8px; text-align: right;'>" . ($product['quantity'] ?? 'N/A') . "</td>";
            $content .= "</tr>";
        }

        $content .= "</table>";

        // Add sync statistics
        $content .= "<h3>Στατιστικά Συγχρονισμού</h3>";
        $content .= "<ul>";
        $content .= "<li>Συνολικά προϊόντα: " . ($syncStats['products_processed'] ?? 0) . "</li>";
        $content .= "<li>Νέα προϊόντα: " . ($syncStats['products_created'] ?? 0) . "</li>";
        $content .= "<li>Ενημερωμένα προϊόντα: " . ($syncStats['products_updated'] ?? 0) . "</li>";
        if (!empty($syncStats['products_errors'])) {
            $content .= "<li style='color: red;'>Σφάλματα: " . $syncStats['products_errors'] . "</li>";
        }
        $content .= "</ul>";

        return str_replace('{{CONTENT}}', $content, $html);
    }

    /**
     * Build error email HTML
     *
     * @param string $errorMessage
     * @param array $context
     * @return string HTML email body
     */
    private function buildErrorEmail($errorMessage, $context) {
        $date = date('d/m/Y H:i');

        $html = $this->getEmailTemplate();
        $html = str_replace('{{TITLE}}', 'Σφάλμα Συγχρονισμού', $html);

        $content = "<h2 style='color: #d32f2f;'>Σφάλμα Συγχρονισμού</h2>";
        $content .= "<p>Εντοπίστηκε σφάλμα κατά τη διάρκεια του συγχρονισμού στις {$date}:</p>";

        $content .= "<div style='background-color: #ffebee; border-left: 4px solid #d32f2f; padding: 16px; margin: 20px 0;'>";
        $content .= "<strong>Μήνυμα Σφάλματος:</strong><br>";
        $content .= htmlspecialchars($errorMessage);
        $content .= "</div>";

        if (!empty($context)) {
            $content .= "<h3>Λεπτομέρειες</h3>";
            $content .= "<pre style='background-color: #f5f5f5; padding: 16px; border-radius: 4px; overflow-x: auto;'>";
            $content .= htmlspecialchars(json_encode($context, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
            $content .= "</pre>";
        }

        $content .= "<p><strong>Παρακαλώ ελέγξτε τα logs για περισσότερες πληροφορίες.</strong></p>";

        return str_replace('{{CONTENT}}', $content, $html);
    }

    /**
     * Build daily summary email HTML
     *
     * @param array $summaryData
     * @return string HTML email body
     */
    private function buildDailySummaryEmail($summaryData) {
        $date = $summaryData['date'] ?? date('d/m/Y');

        $html = $this->getEmailTemplate();
        $html = str_replace('{{TITLE}}', "Ημερήσια Αναφορά - {$date}", $html);

        $content = "<h2>Ημερήσια Αναφορά Συγχρονισμού</h2>";
        $content .= "<p>Αναφορά για την ημερομηνία: <strong>{$date}</strong></p>";

        $content .= "<div style='display: flex; flex-wrap: wrap; gap: 20px; margin: 20px 0;'>";

        // Statistics cards
        $stats = [
            ['label' => 'Συνολικοί Συγχρονισμοί', 'value' => $summaryData['total_syncs'] ?? 0, 'color' => '#1976d2'],
            ['label' => 'Επιτυχημένοι', 'value' => $summaryData['successful_syncs'] ?? 0, 'color' => '#388e3c'],
            ['label' => 'Αποτυχημένοι', 'value' => $summaryData['failed_syncs'] ?? 0, 'color' => '#d32f2f'],
            ['label' => 'Προϊόντα Δημιουργήθηκαν', 'value' => $summaryData['total_products_created'] ?? 0, 'color' => '#f57c00']
        ];

        foreach ($stats as $stat) {
            $content .= "<div style='background-color: {$stat['color']}; color: white; padding: 20px; border-radius: 8px; text-align: center; min-width: 150px;'>";
            $content .= "<div style='font-size: 24px; font-weight: bold;'>{$stat['value']}</div>";
            $content .= "<div style='font-size: 14px;'>{$stat['label']}</div>";
            $content .= "</div>";
        }

        $content .= "</div>";

        // Performance metrics
        if (!empty($summaryData['avg_execution_time'])) {
            $content .= "<h3>Επιδόσεις</h3>";
            $content .= "<ul>";
            $content .= "<li>Μέσος χρόνος εκτέλεσης: " . round($summaryData['avg_execution_time'], 2) . " δευτερόλεπτα</li>";
            if (!empty($summaryData['max_memory_usage'])) {
                $content .= "<li>Μέγιστη χρήση μνήμης: " . $summaryData['max_memory_usage'] . "</li>";
            }
            $content .= "</ul>";
        }

        return str_replace('{{CONTENT}}', $content, $html);
    }

    /**
     * Build test email HTML
     *
     * @return string HTML email body
     */
    private function buildTestEmail() {
        $html = $this->getEmailTemplate();
        $html = str_replace('{{TITLE}}', 'Test Email', $html);

        $content = "<h2>Test Email</h2>";
        $content .= "<p>Αυτό είναι ένα test email από το σύστημα συγχρονισμού SoftOne Go με ATUM.</p>";
        $content .= "<p>Αν λαμβάνετε αυτό το email, η ρύθμιση email λειτουργεί σωστά.</p>";
        $content .= "<p><strong>Χρόνος αποστολής:</strong> " . date('d/m/Y H:i:s') . "</p>";

        return str_replace('{{CONTENT}}', $content, $html);
    }

    /**
     * Get basic HTML email template
     *
     * @return string HTML template
     */
    private function getEmailTemplate() {
        return '
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{{TITLE}}</title>
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }
        .container { max-width: 800px; margin: 0 auto; padding: 20px; }
        .header { background-color: #1976d2; color: white; padding: 20px; text-align: center; }
        .content { padding: 20px; }
        .footer { background-color: #f5f5f5; padding: 20px; text-align: center; font-size: 12px; color: #666; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f5f5f5; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>{{TITLE}}</h1>
        </div>
        <div class="content">
            {{CONTENT}}
        </div>
        <div class="footer">
            <p>Αυτό το email στάλθηκε αυτόματα από το σύστημα συγχρονισμού SoftOne Go - ATUM.<br>
            Παρακαλώ μην απαντήσετε σε αυτό το email.</p>
        </div>
    </div>
</body>
</html>';
    }

    /**
     * Get email queue statistics
     *
     * @return array
     */
    public function getQueueStatistics() {
        if (!$this->database) {
            return [];
        }

        try {
            $stmt = $this->database->query("
                SELECT
                    status,
                    COUNT(*) as count
                FROM email_queue
                GROUP BY status
            ");

            $stats = [];
            while ($row = $stmt->fetch()) {
                $stats[$row['status']] = $row['count'];
            }

            return $stats;

        } catch (Exception $e) {
            $this->logger->warning('Failed to get email queue statistics', [
                'error' => $e->getMessage()
            ]);
            return [];
        }
    }
}