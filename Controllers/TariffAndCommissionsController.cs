using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

[RequireAuth]
[RequirePermission("settings.tariff")]
public class TariffAndCommissionsController : Controller
{
    private readonly AppDbContext _db;

    public TariffAndCommissionsController(AppDbContext db)
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
    /// Тариф и комиссии текущей организации (из сессии).
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var organization = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId);
        if (organization == null)
            return NotFound();

        var settings = await _db.OrganizationSettings
            .Include(s => s.Commission)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

        if (settings == null)
        {
            settings = new OrganizationSettings
            {
                OrganizationId = organizationId,
                DisableInvoiceServiceSelection = false,
                AllowedHassameaccount = false,
                Paymentreminderdaysbefore = 0
            };
        }

        var subscription = await _db.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

        ViewBag.Organization = organization;
        ViewBag.OrganizationId = organizationId;
        ViewBag.Settings = settings;
        ViewBag.Subscription = subscription;
        ViewBag.Commissions = await _db.Commissions.OrderBy(c => c.Name).ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(string organizationId, string? billingType, bool useLowerCommissionFromOrg,
        string? commissionId, decimal? subscriptionPriceSom = null, string? subscriptionPeriodType = null)
    {
        var currentOrganizationId = RequireCurrentOrganizationId(organizationId);
        if (string.IsNullOrEmpty(currentOrganizationId))
            return Unauthorized();

        var isSubscription = string.Equals(billingType, "subscription", StringComparison.OrdinalIgnoreCase);
        var billingTypeToSave = string.IsNullOrWhiteSpace(billingType) ? null : (billingType == "commission" ? null : billingType);

        var existing = await _db.OrganizationSettings.FirstOrDefaultAsync(s => s.OrganizationId == currentOrganizationId);
        if (existing != null)
        {
            existing.BillingType = billingTypeToSave;
            existing.UseLowerCommissionFromOrg = useLowerCommissionFromOrg;
            existing.CommissionId = useLowerCommissionFromOrg && !string.IsNullOrWhiteSpace(commissionId) ? commissionId : null;
        }
        else
        {
            _db.OrganizationSettings.Add(new OrganizationSettings
            {
                OrganizationId = currentOrganizationId,
                BillingType = billingTypeToSave,
                UseLowerCommissionFromOrg = useLowerCommissionFromOrg,
                CommissionId = useLowerCommissionFromOrg && !string.IsNullOrWhiteSpace(commissionId) ? commissionId : null
            });
        }

        if (isSubscription && subscriptionPriceSom.HasValue && subscriptionPriceSom >= 0)
        {
            var sub = await _db.OrganizationSubscriptions.FirstOrDefaultAsync(s => s.OrganizationId == currentOrganizationId);
            if (sub == null)
            {
                sub = new OrganizationSubscription
                {
                    Id = Guid.NewGuid().ToString(),
                    OrganizationId = currentOrganizationId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.OrganizationSubscriptions.Add(sub);
            }
            sub.PriceTyiyn = subscriptionPriceSom.Value * 100m;
            sub.PeriodType = subscriptionPeriodType == "year" ? "year" : "month";
            sub.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Настройки тарифа и комиссий сохранены.";
        return RedirectToAction(nameof(Index));
    }
}
