namespace WebApplication1.Models.DBModels;

/// <summary>
/// Настройки подписочного тарифа организации (1:1 при BillingType = subscription).
/// Стоимость и период действия подписки.
/// </summary>
public class OrganizationSubscription
{
    public string Id { get; set; } = null!;

    public string OrganizationId { get; set; } = null!;

    
    public decimal PriceTyiyn { get; set; }

    /// <summary>Период: month — 1 месяц, year — 12 месяцев.</summary>
    public string PeriodType { get; set; } = "month";

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}
