namespace WebApplication1.Services;

public sealed class NotificationBatchCreateResult
{
    public int SelectedClients { get; set; }

    public int ClientsQueued { get; set; }

    public int ClientsSkippedWithoutChannel { get; set; }

    public int CreatedNotifications { get; set; }

    public int EmailNotifications { get; set; }

    public int TelegramNotifications { get; set; }

    public int WhatsAppNotifications { get; set; }
}
