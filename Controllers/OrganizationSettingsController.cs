using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

[RequireAuth]
public class OrganizationSettingsController : Controller
{
    private readonly AppDbContext _db;

    public OrganizationSettingsController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetCurrentOrganizationId()
    {
        return AuthorizationHelper.GetOrganizationId(HttpContext);
    }

    public async Task<IActionResult> Index()
    {
        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var settings = await _db.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

        if (settings == null)
        {
            settings = new OrganizationSettings
            {
                OrganizationId = organizationId,
                DisableInvoiceServiceSelection = false,
                AllowedHassameaccount = false,
                InvoicePayCodeMode = null,
                Paymentreminderdaysbefore = 0
            };
        }

        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OrganizationSettings model)
    {
        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var existing = await _db.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

        if (existing != null)
        {
            existing.DisableInvoiceServiceSelection = model.DisableInvoiceServiceSelection;
            existing.InvoicePayCodeMode = model.InvoicePayCodeMode;
            existing.AllowedHassameaccount = string.IsNullOrEmpty(model.InvoicePayCodeMode) ? model.AllowedHassameaccount : (model.InvoicePayCodeMode != "new_only");
            existing.Paymentreminderdaysbefore = model.Paymentreminderdaysbefore;
        }
        else
        {
            model.OrganizationId = organizationId;
            _db.OrganizationSettings.Add(model);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Настройки сохранены.";
        return RedirectToAction(nameof(Index));
    }
}
