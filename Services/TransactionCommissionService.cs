using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services;

/// <summary>
/// Расчёт трёх видов комиссий для транзакции. Все считаются от суммы транзакции.
/// Не включённые в настройках виды возвращаются как 0 (в транзакции сохраняется 0).
/// </summary>
public class TransactionCommissionService
{
    private readonly AppDbContext _db;

    public TransactionCommissionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Вычисляет все три комиссии от суммы транзакции (amountTyiyn). Чекбокс не выставлен — 0.
    /// </summary>
    public async Task<TransactionCommissionResult> GetCommissionsForTransactionAsync(
        string? organizationId,
        string agentId,
        decimal amountTyiyn,
        CancellationToken cancellationToken = default)
    {
        var result = new TransactionCommissionResult(); // 0, 0, 0
        if (string.IsNullOrEmpty(organizationId) || amountTyiyn <= 0)
            return result;

        var settings = await _db.OrganizationSettings
            .AsNoTracking()
            .Include(s => s.Commission)
            .ThenInclude(c => c!.Tiers.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);

        if (settings == null)
            return result;

        // Нижняя комиссия от организации — от суммы транзакции; если чекбокс не выставлен — остаётся 0
        if (settings.UseLowerCommissionFromOrg && settings.CommissionId != null && settings.Commission != null)
            result.LowerCommissionFromOrg = CommissionCalculator.CalculateFee(settings.Commission, amountTyiyn);

        // Верхняя и нижняя к агенту — из agent_commission
        if (settings.UseUpperCommissionFromAgent || settings.UseLowerCommissionToAgent)
        {
            var agentCommission = await _db.AgentCommissions
                .Include(a => a.Commission)
                .ThenInclude(c => c!.Tiers.OrderBy(t => t.SortOrder))
                .Include(a => a.LowerCommission)
                .ThenInclude(c => c!.Tiers.OrderBy(t => t.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.AgentId == agentId && a.OrganizationId == organizationId,
                    cancellationToken);

            if (settings.UseUpperCommissionFromAgent && agentCommission?.Commission != null)
                result.UpperCommissionFromAgent = CommissionCalculator.CalculateFee(agentCommission.Commission, amountTyiyn);

            if (settings.UseLowerCommissionToAgent && agentCommission?.LowerCommission != null)
                result.LowerCommissionToAgent = CommissionCalculator.CalculateFee(agentCommission.LowerCommission, amountTyiyn);
        }

        return result;
    }
}
