namespace WebApplication1.Models.DBModels;

/// <summary>
/// Справочник видов комиссии (верхняя комиссия с клиента).
/// Виды: percent, fixed, mixed, single_tier, progressive.
/// Ограничения: min_fee, max_fee.
/// </summary>
public class Commission
{
    public string Id { get; set; } = null!;

    /// <summary>Название (для выбора в настройках организации).</summary>
    public string? Name { get; set; }

    /// <summary>percent | fixed | mixed | single_tier | progressive</summary>
    public string CommissionKind { get; set; } = null!;

    /// <summary>Процент (0.02 = 2%). Для percent, mixed, и ступеней.</summary>
    public decimal? Rate { get; set; }

    /// <summary>Фиксированная сумма в сомах. Для fixed, mixed.</summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>Минимальная комиссия в сомах (clamp).</summary>
    public decimal? MinFee { get; set; }

    /// <summary>Максимальная комиссия в сомах (clamp).</summary>
    public decimal? MaxFee { get; set; }

    public virtual ICollection<CommissionTier> Tiers { get; set; } = new List<CommissionTier>();
    public virtual ICollection<AgentCommission> AgentCommissions { get; set; } = new List<AgentCommission>();
}
