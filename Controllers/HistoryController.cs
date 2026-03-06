using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

[RequireAuth]
public class HistoryController : Controller
{
    private readonly AppDbContext _db;

    public HistoryController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetCurrentOrganizationId()
    {
        return AuthorizationHelper.GetOrganizationId(HttpContext);
    }

    public async Task<IActionResult> Index(int maxCount = 500)
    {
        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var invoiceIds = await _db.Invoices
            .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
            .Select(i => i.Id)
            .ToListAsync();

        var list = await _db.Histories
            .Where(h => h.Invoice == null || invoiceIds.Contains(h.Invoice))
            .Take(maxCount)
            .ToListAsync();

        return View(list);
    }
}
