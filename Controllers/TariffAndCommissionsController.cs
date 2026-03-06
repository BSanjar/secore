using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

[RequireAuth]
public class TariffAndCommissionsController : Controller
{
    private readonly AppDbContext _db;

    public TariffAndCommissionsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Страница «Тариф и комиссии». Сверху — выбор организации; настройки по выбранной организации.
    /// При открытии не привязываемся к организации текущего пользователя.
    /// </summary>
    public async Task<IActionResult> Index(string? organizationId)
    {
        ViewBag.Organizations = await _db.Organizations.OrderBy(o => o.Name).ToListAsync();
        ViewBag.SelectedOrganizationId = organizationId;

        if (string.IsNullOrEmpty(organizationId))
        {
            ViewBag.Settings = null;
            ViewBag.Commissions = await _db.Commissions.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Agents = await _db.Agents.OrderBy(a => a.Name).ToListAsync();
            ViewBag.AgentCommissions = Array.Empty<AgentCommission>();
            return View();
        }

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

        ViewBag.Settings = settings;
        ViewBag.Commissions = await _db.Commissions.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Agents = await _db.Agents.OrderBy(a => a.Name).ToListAsync();
        ViewBag.AgentCommissions = await _db.AgentCommissions
            .Include(ac => ac.Agent)
            .Include(ac => ac.Commission)
            .Include(ac => ac.LowerCommission)
            .Where(ac => ac.OrganizationId == organizationId)
            .OrderBy(ac => ac.Agent!.Name)
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(string organizationId, string? billingType, bool useLowerCommissionFromOrg,
        string? commissionId, bool useUpperCommissionFromAgent, bool useLowerCommissionToAgent)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            TempData["Error"] = "Организация не выбрана.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await _db.OrganizationSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId);
        if (existing != null)
        {
            existing.BillingType = string.IsNullOrWhiteSpace(billingType) ? null : billingType;
            existing.UseLowerCommissionFromOrg = useLowerCommissionFromOrg;
            existing.CommissionId = useLowerCommissionFromOrg && !string.IsNullOrWhiteSpace(commissionId) ? commissionId : null;
            existing.UseUpperCommissionFromAgent = useUpperCommissionFromAgent;
            existing.UseLowerCommissionToAgent = useLowerCommissionToAgent;
        }
        else
        {
            _db.OrganizationSettings.Add(new OrganizationSettings
            {
                OrganizationId = organizationId,
                BillingType = string.IsNullOrWhiteSpace(billingType) ? null : billingType,
                UseLowerCommissionFromOrg = useLowerCommissionFromOrg,
                CommissionId = useLowerCommissionFromOrg && !string.IsNullOrWhiteSpace(commissionId) ? commissionId : null,
                UseUpperCommissionFromAgent = useUpperCommissionFromAgent,
                UseLowerCommissionToAgent = useLowerCommissionToAgent
            });
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Настройки тарифа и комиссий сохранены.";
        return RedirectToAction(nameof(Index), new { organizationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAgentCommission(string organizationId, string agentId, string commissionId, string? lowerCommissionId)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            TempData["Error"] = "Организация не выбрана.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(commissionId))
        {
            TempData["Error"] = "Выберите агента и верхнюю комиссию.";
            return RedirectToAction(nameof(Index), new { organizationId });
        }
        var existing = await _db.AgentCommissions
            .FirstOrDefaultAsync(ac => ac.AgentId == agentId && ac.OrganizationId == organizationId);
        if (existing != null)
        {
            existing.CommissionId = commissionId;
            existing.LowerCommissionId = string.IsNullOrWhiteSpace(lowerCommissionId) ? null : lowerCommissionId;
        }
        else
        {
            _db.AgentCommissions.Add(new AgentCommission
            {
                Id = Guid.NewGuid().ToString(),
                AgentId = agentId,
                OrganizationId = organizationId,
                CommissionId = commissionId,
                LowerCommissionId = string.IsNullOrWhiteSpace(lowerCommissionId) ? null : lowerCommissionId
            });
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Комиссия по агенту сохранена.";
        return RedirectToAction(nameof(Index), new { organizationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAgentCommission(string organizationId, string id)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            TempData["Error"] = "Организация не выбрана.";
            return RedirectToAction(nameof(Index));
        }
        var ac = await _db.AgentCommissions
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId);
        if (ac != null)
        {
            _db.AgentCommissions.Remove(ac);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Комиссия по агенту удалена.";
        }
        return RedirectToAction(nameof(Index), new { organizationId });
    }
}
