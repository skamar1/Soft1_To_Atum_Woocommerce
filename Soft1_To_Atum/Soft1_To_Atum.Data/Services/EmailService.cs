using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Data.Services;

public class EmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> TestEmailSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing email settings for SMTP host: {SmtpHost}:{SmtpPort}",
                settings.EmailSmtpHost, settings.EmailSmtpPort);

            if (string.IsNullOrEmpty(settings.EmailSmtpHost) ||
                string.IsNullOrEmpty(settings.EmailUsername) ||
                string.IsNullOrEmpty(settings.EmailFromEmail) ||
                string.IsNullOrEmpty(settings.EmailToEmail))
            {
                _logger.LogWarning("Email settings are incomplete");
                return false;
            }

            using var client = CreateSmtpClient(settings);

            var testMessage = new MailMessage
            {
                From = new MailAddress(settings.EmailFromEmail),
                Subject = "Test Email - SoftOne to ATUM Sync",
                Body = $"This is a test email sent at {DateTime.Now:yyyy-MM-dd HH:mm:ss} to verify email configuration.",
                IsBodyHtml = false
            };

            testMessage.To.Add(settings.EmailToEmail);

            await client.SendMailAsync(testMessage, cancellationToken);
            _logger.LogInformation("Test email sent successfully to {ToEmail}", settings.EmailToEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email: {Message}", ex.Message);
            return false;
        }
    }

    public async Task SendSyncNotificationAsync(SyncLog syncLog, AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!settings.SyncEmailNotifications || string.IsNullOrEmpty(settings.EmailToEmail))
            {
                _logger.LogDebug("Email notifications are disabled or no recipient configured");
                return;
            }

            _logger.LogDebug("Sending sync notification email for sync log {SyncLogId}", syncLog.Id);

            using var client = CreateSmtpClient(settings);

            var subject = syncLog.Status == "Completed"
                ? $"✅ Sync Completed - {settings.StoreName}"
                : $"❌ Sync Failed - {settings.StoreName}";

            var body = BuildSyncNotificationBody(syncLog, settings);

            var message = new MailMessage
            {
                From = new MailAddress(settings.EmailFromEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(settings.EmailToEmail);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Sync notification email sent successfully to {ToEmail} for sync {SyncLogId}",
                settings.EmailToEmail, syncLog.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sync notification email: {Message}", ex.Message);
            throw;
        }
    }

    private SmtpClient CreateSmtpClient(AppSettings settings)
    {
        var client = new SmtpClient(settings.EmailSmtpHost, settings.EmailSmtpPort)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.EmailUsername, settings.EmailPassword)
        };

        return client;
    }

    private string BuildSyncNotificationBody(SyncLog syncLog, AppSettings settings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine(".header { background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px; }");
        sb.AppendLine(".success { color: #28a745; }");
        sb.AppendLine(".error { color: #dc3545; }");
        sb.AppendLine(".stats { background-color: #e9ecef; padding: 15px; border-radius: 5px; }");
        sb.AppendLine(".stats-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; }");
        sb.AppendLine(".stat-item { background-color: white; padding: 10px; border-radius: 3px; text-align: center; }");
        sb.AppendLine(".stat-value { font-size: 24px; font-weight: bold; }");
        sb.AppendLine(".stat-label { font-size: 12px; color: #6c757d; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<h2>Sync Report - {settings.StoreName}</h2>");
        sb.AppendLine($"<p><strong>Started:</strong> {syncLog.StartedAt:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"<p><strong>Completed:</strong> {syncLog.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}</p>");
        sb.AppendLine($"<p><strong>Duration:</strong> {syncLog.Duration?.ToString(@"hh\:mm\:ss") ?? "N/A"}</p>");

        if (syncLog.Status == "Completed")
        {
            sb.AppendLine("<p class='success'><strong>Status: ✅ Completed Successfully</strong></p>");
        }
        else
        {
            sb.AppendLine("<p class='error'><strong>Status: ❌ Failed</strong></p>");
            if (!string.IsNullOrEmpty(syncLog.ErrorDetails))
            {
                sb.AppendLine($"<p class='error'><strong>Error:</strong> {syncLog.ErrorDetails}</p>");
            }
        }
        sb.AppendLine("</div>");

        // Statistics
        sb.AppendLine("<div class='stats'>");
        sb.AppendLine("<h3>Sync Statistics</h3>");
        sb.AppendLine("<div class='stats-grid'>");

        sb.AppendLine("<div class='stat-item'>");
        sb.AppendLine($"<div class='stat-value'>{syncLog.TotalProducts}</div>");
        sb.AppendLine("<div class='stat-label'>Total Products</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='stat-item'>");
        sb.AppendLine($"<div class='stat-value' style='color: #28a745;'>{syncLog.CreatedProducts}</div>");
        sb.AppendLine("<div class='stat-label'>Created</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='stat-item'>");
        sb.AppendLine($"<div class='stat-value' style='color: #17a2b8;'>{syncLog.UpdatedProducts}</div>");
        sb.AppendLine("<div class='stat-label'>Updated</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='stat-item'>");
        sb.AppendLine($"<div class='stat-value' style='color: #ffc107;'>{syncLog.SkippedProducts}</div>");
        sb.AppendLine("<div class='stat-label'>Skipped</div>");
        sb.AppendLine("</div>");

        if (syncLog.ErrorCount > 0)
        {
            sb.AppendLine("<div class='stat-item'>");
            sb.AppendLine($"<div class='stat-value' style='color: #dc3545;'>{syncLog.ErrorCount}</div>");
            sb.AppendLine("<div class='stat-label'>Errors</div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div>");
        sb.AppendLine("</div>");

        // Configuration info
        sb.AppendLine("<div style='margin-top: 20px; font-size: 12px; color: #6c757d;'>");
        sb.AppendLine("<h4>Configuration</h4>");
        sb.AppendLine($"<p><strong>SoftOne URL:</strong> {settings.SoftOneGoBaseUrl}</p>");
        sb.AppendLine($"<p><strong>WooCommerce URL:</strong> {settings.WooCommerceUrl}</p>");
        sb.AppendLine($"<p><strong>ATUM Location:</strong> {settings.AtumLocationName} (ID: {settings.AtumLocationId})</p>");
        sb.AppendLine($"<p><strong>Filters:</strong> {settings.SoftOneGoFilters}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div style='margin-top: 20px; padding: 10px; background-color: #f8f9fa; border-radius: 5px; font-size: 12px;'>");
        sb.AppendLine("<p>This is an automated message from the SoftOne to ATUM synchronization service.</p>");
        sb.AppendLine($"<p>Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}