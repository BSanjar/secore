namespace WebApplication1.Services;

/// <summary>
/// Три вида комиссий по транзакции (в тыйынах), все считаются от суммы транзакции.
/// Если вид комиссии не включён в настройках — в транзакции сохраняется 0.
/// </summary>
public class TransactionCommissionResult
{
    /// <summary>Нижняя комиссия от организации (0 если чекбокс не выставлен).</summary>
    public decimal LowerCommissionFromOrg { get; set; }

    /// <summary>Верхняя комиссия от агента (0 если не выставлена).</summary>
    public decimal UpperCommissionFromAgent { get; set; }

    /// <summary>Нижняя комиссия к агенту (0 если не выставлена).</summary>
    public decimal LowerCommissionToAgent { get; set; }
}
