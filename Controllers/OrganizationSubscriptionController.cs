using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

/// <summary>
/// Настройка подписки текущей организации, просмотр статуса доступа и истории оплат.
/// </summary>
[RequireAuth]
public class OrganizationSubscriptionController : Controller
{
    private readonly AppDbContext _db;

    public OrganizationSubscriptionController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetCurrentOrganizationId()
    {
        return AuthorizationHelper.GetOrganizationId(HttpContext);
    }

    private string? RequireCurrentOrganizationId(string? organizationId)
    {
        var current = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(current))
            return null;

        if (!string.IsNullOrWhiteSpace(organizationId)
            && !string.Equals(current, organizationId.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        return current;
    }

    /// <summary>
    /// Страница подписки: настройки тарифа, текущий статус доступа, история оплат.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var orgId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(orgId))
            return RedirectToAction("Login", "Account");

        var org = await _db.Organizations
            .Include(o => o.Settings)
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == orgId);
        if (org == null)
            return NotFound();

        var today = DateTime.UtcNow.Date;
        var periodEnd = await SubscriptionHelper.GetCurrentPeriodEndAsync(_db, orgId, today);
        var payments = await _db.OrganizationSubscriptionPayments
            .Where(p => p.OrganizationId == orgId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        ViewBag.Organization = org;
        ViewBag.OrganizationId = orgId;
        ViewBag.IsSubscriptionBilling = string.Equals(org.Settings?.BillingType, "subscription", StringComparison.OrdinalIgnoreCase);
        ViewBag.CurrentPeriodEnd = periodEnd;
        ViewBag.IsActive = org.IsActive;
        ViewBag.Payments = payments;
        ViewBag.Subscription = org.Subscription;

        return View();
    }

    /// <summary>
    /// Сохранение настроек тарифа подписки (стоимость, период). Устанавливает BillingType = subscription.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(string organizationId, decimal priceSom, string periodType)
    {
        var orgId = RequireCurrentOrganizationId(organizationId);
        if (string.IsNullOrEmpty(orgId))
            return Unauthorized();

        var settings = await _db.OrganizationSettings.FirstOrDefaultAsync(s => s.OrganizationId == orgId);
        if (settings == null)
        {
            settings = new OrganizationSettings { OrganizationId = orgId };
            _db.OrganizationSettings.Add(settings);
        }

        settings.BillingType = "subscription";

        var sub = await _db.OrganizationSubscriptions.FirstOrDefaultAsync(s => s.OrganizationId == orgId);
        if (sub == null)
        {
            sub = new OrganizationSubscription
            {
                Id = Guid.NewGuid().ToString(),
                OrganizationId = orgId,
                CreatedAt = DateTime.UtcNow
            };
            _db.OrganizationSubscriptions.Add(sub);
        }

        sub.PriceTyiyn = priceSom * 100m;
        sub.PeriodType = periodType == "year" ? "year" : "month";
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["SubscriptionMessage"] = "Настройки подписки сохранены.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Добавление оплаты подписки (оплаченный период).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(string organizationId, DateTime periodStart, DateTime periodEnd, decimal amountSom, string? note = null)
    {
        var orgId = RequireCurrentOrganizationId(organizationId);
        if (string.IsNullOrEmpty(orgId))
            return Unauthorized();

        if (periodStart > periodEnd)
        {
            TempData["SubscriptionError"] = "Дата начала периода не может быть позже даты окончания.";
            return RedirectToAction(nameof(Index));
        }

        var payment = new OrganizationSubscriptionPayment
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationId = orgId,
            PaidAt = DateTime.UtcNow,
            AmountTyiyn = amountSom * 100m,
            PeriodStart = periodStart.Date,
            PeriodEnd = periodEnd.Date,
            Note = note,
            CreatedAt = DateTime.UtcNow
        };
        _db.OrganizationSubscriptionPayments.Add(payment);

        var org = await _db.Organizations.FindAsync(orgId);
        if (org != null && !org.IsActive && periodEnd.Date >= DateTime.UtcNow.Date)
            org.IsActive = true;

        await _db.SaveChangesAsync();
        TempData["SubscriptionMessage"] = "Оплата добавлена. Период доступа обновлён.";
        return RedirectToAction(nameof(Index));
    }
}
