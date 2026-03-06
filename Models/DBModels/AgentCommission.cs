namespace WebApplication1.Models.DBModels;

/// <summary>
/// Связь агента и организации с видом комиссии.
/// У одной организации может быть несколько агентов, у каждого — своя комиссия (верхняя от агента / нижняя к агенту).
/// </summary>
public class AgentCommission
{
    public string Id { get; set; } = null!;

    /// <summary>Агент (приём платежей).</summary>
    public string AgentId { get; set; } = null!;

    /// <summary>Организация (счета и клиенты).</summary>
    public string OrganizationId { get; set; } = null!;

    /// <summary>Верхняя комиссия от агента (вид из справочника commission).</summary>
    public string CommissionId { get; set; } = null!;

    /// <summary>Нижняя комиссия к агенту (вид из справочника commission). Опционально.</summary>
    public string? LowerCommissionId { get; set; }

    public virtual Agent Agent { get; set; } = null!;
    public virtual Organization Organization { get; set; } = null!;
    public virtual Commission Commission { get; set; } = null!;
    public virtual Commission? LowerCommission { get; set; }
}
