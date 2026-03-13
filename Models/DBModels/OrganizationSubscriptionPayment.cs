namespace WebApplication1.Models.DBModels;

/// <summary>
/// Оплата подписки организации. Каждая запись — один оплаченный период (диапазон дат доступа).
/// Поддерживается оплата на несколько месяцев или год вперёд.
/// </summary>
public class OrganizationSubscriptionPayment
{
    public string Id { get; set; } = null!;

    public string OrganizationId { get; set; } = null!;

    /// <summary>Дата и время оплаты.</summary>
    public DateTime PaidAt { get; set; }

    /// <summary>Сумма оплаты в тыйынах.</summary>
    public decimal AmountTyiyn { get; set; }

    /// <summary>Начало покрытого периода (дата включительно).</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>Конец покрытого периода (дата включительно).</summary>
    public DateTime PeriodEnd { get; set; }

    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}
