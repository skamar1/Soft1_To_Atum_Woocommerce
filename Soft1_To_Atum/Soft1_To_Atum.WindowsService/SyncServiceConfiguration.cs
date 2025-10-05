namespace Soft1_To_Atum.WindowsService;

public class SyncServiceConfiguration
{
    public SoftOneSettings SoftOne { get; set; } = new();
    public WooCommerceSettings WooCommerce { get; set; } = new();
    public AtumSettings ATUM { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public SyncSettings SyncSettings { get; set; } = new();
}

public class SoftOneSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string S1Code { get; set; } = string.Empty;
}

public class WooCommerceSettings
{
    public string ConsumerKey { get; set; } = string.Empty;
    public string ConsumerSecret { get; set; } = string.Empty;
}

public class AtumSettings
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public bool EnableNotifications { get; set; } = true;
}

public class SyncSettings
{
    public int IntervalMinutes { get; set; } = 10;
    public bool EnableAutoSync { get; set; } = true;
    public int BatchSize { get; set; } = 50;
}
