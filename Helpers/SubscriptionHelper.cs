using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Helpers;

/// <summary>
/// Проверка доступа организации. Доступ разрешён только если у организации IsActive = true.
/// </summary>
public static class SubscriptionHelper
{
    /// <summary>
    /// Проверка доступа: только по флагу is_active организации (без проверки подписки и платежей).
    /// </summary>
    public static async Task<bool> HasSubscriptionAccessAsync(AppDbContext db, string organizationId, DateTime? date = null, CancellationToken ct = default)
    {
        var org = await db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        return org != null && org.IsActive;
    }

    /// <summary>
    /// Возвращает дату окончания текущего оплаченного периода (включительно) или null.
    /// </summary>
    public static async Task<DateTime?> GetCurrentPeriodEndAsync(AppDbContext db, string organizationId, DateTime? date = null, CancellationToken ct = default)
    {
        var checkDate = (date ?? DateTime.UtcNow).Date;
        var payment = await db.OrganizationSubscriptionPayments
            .Where(p => p.OrganizationId == organizationId && p.PeriodStart <= checkDate && p.PeriodEnd >= checkDate)
            .OrderByDescending(p => p.PeriodEnd)
            .Select(p => new { p.PeriodEnd })
            .FirstOrDefaultAsync(ct);
        return payment?.PeriodEnd;
    }
}
