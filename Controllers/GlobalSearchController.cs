using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers;

[RequireAuth]
public class GlobalSearchController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _currentTenantService;

    public GlobalSearchController(AppDbContext db, ICurrentTenantService currentTenantService)
    {
        _db = db;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Suggest(string q)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Clients) || string.IsNullOrWhiteSpace(q))
            return Json(new { groups = Array.Empty<object>() });

        var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
        if (string.IsNullOrEmpty(organizationId))
            return Json(new { groups = Array.Empty<object>() });

        q = q.Trim();
        var like = $"%{q}%";

        var clients = await _db.OrganizationClients
            .Where(c =>
                c.Organization == organizationId &&
                (
                    (!string.IsNullOrEmpty(c.ClientName) && EF.Functions.ILike(c.ClientName, like)) ||
                    (!string.IsNullOrEmpty(c.Id) && EF.Functions.ILike(c.Id, like))
                ))
            .OrderBy(c => c.ClientName)
            .Take(5)
            .Select(c => new
            {
                title = c.ClientName ?? c.Id,
                subtitle = "Клиент",
                url = Url.Action("Index", "Clients", new { search = c.ClientName ?? c.Id })
            })
            .ToListAsync();

        var invoices = await _db.Invoices
            .Include(i => i.ClientNavigation)
            .Where(i =>
                i.ClientNavigation != null &&
                i.ClientNavigation.Organization == organizationId &&
                (
                    (!string.IsNullOrEmpty(i.PayCode) && EF.Functions.ILike(i.PayCode, like)) ||
                    (!string.IsNullOrEmpty(i.NameInvoice) && EF.Functions.ILike(i.NameInvoice, like))
                ))
            .OrderByDescending(i => i.DateCreated)
            .Take(5)
            .Select(i => new
            {
                title = i.NameInvoice ?? i.PayCode ?? i.Id,
                subtitle = $"Счёт {i.PayCode ?? i.Id}",
                url = Url.Action("Details", "Invoices", new { id = i.Id })
            })
            .ToListAsync();

        var groups = new[]
        {
            new { title = "Клиенты", items = clients },
            new { title = "Счета", items = invoices }
        }
        .Where(g => g.items.Any())
        .ToList();

        return Json(new { groups });
    }
}
