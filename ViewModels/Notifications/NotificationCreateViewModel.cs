using System.ComponentModel.DataAnnotations;

using WebApplication1.Services;

namespace WebApplication1.ViewModels.Notifications;

public sealed class NotificationCreateViewModel
{
    [Required(ErrorMessage = "Укажите тему уведомления.")]
    [StringLength(200, ErrorMessage = "Тема не должна превышать 200 символов.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите текст уведомления.")]
    [StringLength(4000, ErrorMessage = "Текст не должен превышать 4000 символов.")]
    public string Message { get; set; } = string.Empty;

    public bool SendEmail { get; set; } = true;

    public bool SendTelegram { get; set; }

    public bool SendWhatsApp { get; set; }

    public List<string> SelectedClientIds { get; set; } = new();

    public List<string> SelectedGroupIds { get; set; } = new();

    /// <summary>
    /// Режим получателей: all или manual.
    /// </summary>
    public string RecipientMode { get; set; } = NotificationRecipientModes.All;

    /// <summary>
    /// Фильтр по полу: пусто, male, female.
    /// </summary>
    public string GenderFilter { get; set; } = string.Empty;

    /// <summary>
    /// Фильтр по возрасту (из даты рождения): under18, 18_30, under30, 30plus, 40plus.
    /// </summary>
    public string AgeFilter { get; set; } = string.Empty;

    public string ClientSearch { get; set; } = string.Empty;

    public string GroupSearch { get; set; } = string.Empty;

    public IReadOnlyList<NotificationRecipientOptionViewModel> AvailableClients { get; set; } = Array.Empty<NotificationRecipientOptionViewModel>();

    public IReadOnlyList<NotificationGroupOptionViewModel> AvailableGroups { get; set; } = Array.Empty<NotificationGroupOptionViewModel>();
}

public sealed class NotificationRecipientOptionViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? GroupId { get; set; }

    public string? GroupName { get; set; }

    public string StatusLabel { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Telegram { get; set; }

    public string? WhatsApp { get; set; }

    public bool HasAnyChannel =>
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(Telegram) ||
        !string.IsNullOrWhiteSpace(WhatsApp);

    public string? Gender { get; set; }

    public int? Age { get; set; }
}

public sealed class NotificationGroupOptionViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int ClientCount { get; set; }
}
