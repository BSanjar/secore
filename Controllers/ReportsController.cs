using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Reports;

namespace WebApplication1.Controllers;

[RequireAuth]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _currentTenantService;

    public ReportsController(AppDbContext db, ICurrentTenantService currentTenantService)
    {
        _db = db;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Reports))
            return NotFound();

        var organizationId = tenant.OrganizationId;
        if (string.IsNullOrWhiteSpace(organizationId))
            return Unauthorized();

        var patientsCount = await _db.OrganizationClients
            .AsNoTracking()
            .CountAsync(x => x.Organization == organizationId);

        var doctorsCount = await _db.Users
            .AsNoTracking()
            .CountAsync(x => x.Organization == organizationId && (x.Isdeleted == null || x.Isdeleted == 0));

        var servicesCount = await _db.OrganizationServices
            .AsNoTracking()
            .CountAsync(x => x.Organization == organizationId && x.Isdeleted != 1);

        var transactionsAmount = await _db.Transactions
            .AsNoTracking()
            .Include(x => x.InvoiceNavigation)
                .ThenInclude(x => x!.ClientNavigation)
            .Where(x =>
                x.TransactionStatus == "success" &&
                x.InvoiceNavigation != null &&
                x.InvoiceNavigation.ClientNavigation != null &&
                x.InvoiceNavigation.ClientNavigation.Organization == organizationId)
            .SumAsync(x => (decimal?)x.Summ) ?? 0m;

        return View(new ReportsIndexViewModel
        {
            PatientsCount = patientsCount,
            DoctorsCount = doctorsCount,
            ServicesCount = servicesCount,
            TransactionsAmount = transactionsAmount / 100m
        });
    }
}
