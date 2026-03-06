namespace WebApplication1.Models.DBModels;

/// <summary>
/// Ступень комиссии для single_tier и progressive.
/// amount_from включительно, amount_to включительно (или +inf для последней ступени).
/// Суммы в сомах.
/// </summary>
public class CommissionTier
{
    public string Id { get; set; } = null!;

    public string CommissionId { get; set; } = null!;

    /// <summary>Нижняя граница диапазона (сомы), включительно.</summary>
    public decimal AmountFrom { get; set; }

    /// <summary>Верхняя граница диапазона (сомы), включительно. Для последней ступени можно ставить большое число.</summary>
    public decimal AmountTo { get; set; }

    /// <summary>Ставка для этого диапазона (0.02 = 2%).</summary>
    public decimal Rate { get; set; }

    /// <summary>Порядок ступени (1, 2, 3...).</summary>
    public int SortOrder { get; set; }

    public virtual Commission Commission { get; set; } = null!;
}
