namespace WebApplication1.Models.DBModels;

/// <summary>
/// Настройки организации (1:1 с Organization).
/// Используется во всех Area. Новые параметры добавлять сюда.
/// </summary>
public class OrganizationSettings
{
    /// <summary>
    /// PK и FK на organization.id.
    /// </summary>
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// Если true, при создании счета отключен выбор услуги:
    /// в таблицу подставляется название счета, доступен только ввод цены.
    /// </summary>
    public bool DisableInvoiceServiceSelection { get; set; }

    /// <summary>
    /// Если true, организации могут создавать счета с одинаковыми л/с.
    /// </summary>
    public bool AllowedHassameaccount { get; set; }

    /// <summary>
    /// Режим выбора лицевого счета при создании счета: new_only, duplicate_only, both.
    /// Если null, используется AllowedHassameaccount (true -> both, false -> new_only).
    /// </summary>
    public string? InvoicePayCodeMode { get; set; }

    /// <summary>
    /// За сколько дней до срока начинать отправку напоминаний по оплате.
    /// </summary>
    public int Paymentreminderdaysbefore { get; set; }

    /// <summary>
    /// Email организации для уведомлений и контактов.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Номер телефона WhatsApp.
    /// </summary>
    public string? WhatsappPhone { get; set; }

    /// <summary>
    /// Номер телефона для обратной связи.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// ФИО директора организации.
    /// </summary>
    public string? DirectorFullName { get; set; }

    /// <summary>
    /// Адрес организации.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Путь/URL логотипа организации.
    /// </summary>
    public string? LogoPath { get; set; }

    /// <summary>
    /// Вид тарифа: subscription (подписка) или комбинация комиссий через флаги ниже.
    /// </summary>
    public string? BillingType { get; set; }

    /// <summary>
    /// Режим QR по умолчанию для организации.
    /// </summary>
    public string? DefaultQrMode { get; set; }

    /// <summary>
    /// Учитывать нижнюю комиссию от организации (от оборота).
    /// </summary>
    public bool UseLowerCommissionFromOrg { get; set; }

    /// <summary>
    /// Справочник комиссии для нижней от организации (расчет от оборота за период).
    /// </summary>
    public string? CommissionId { get; set; }

    /// <summary>
    /// Учитывать верхнюю комиссию от агента (сверху суммы, из agent_commission).
    /// </summary>
    public bool UseUpperCommissionFromAgent { get; set; }

    /// <summary>
    /// Учитывать нижнюю комиссию к агенту (из суммы, из agent_commission).
    /// </summary>
    public bool UseLowerCommissionToAgent { get; set; }

    public virtual Organization Organization { get; set; } = null!;
    public virtual Commission? Commission { get; set; }
}
