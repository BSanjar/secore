using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Patients;

namespace WebApplication1.Controllers;

[RequireAuth]
public class PatientsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ClientService _clientService;
    private readonly ICurrentTenantService _currentTenantService;

    public PatientsController(
        AppDbContext db,
        ClientService clientService,
        ICurrentTenantService currentTenantService)
    {
        _db = db;
        _clientService = clientService;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Patients))
            return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
            return Unauthorized();

        var query = _db.OrganizationClients
            .AsNoTracking()
            .Where(x => x.Organization == tenant.OrganizationId && (x.ClientStatus == null || x.ClientStatus >= 0));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                (x.ClientName != null && x.ClientName.ToLower().Contains(term)) ||
                (x.ClientPhone != null && x.ClientPhone.Contains(search)) ||
                (x.ClientEmail != null && x.ClientEmail.ToLower().Contains(term)) ||
                (x.ClientInn != null && x.ClientInn.Contains(search)));
        }

        var patients = await query
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new PatientListItemViewModel
            {
                Id = x.Id,
                Name = x.ClientName ?? "Без имени",
                Identifier = x.ClientInn,
                Phone = x.ClientPhone,
                Email = x.ClientEmail,
                Address = x.ClientAddress,
                CreatedDate = x.CreatedDate,
                StatusLabel = x.ClientStatus == 1 ? "Активный" :
                    x.ClientStatus == 0 ? "Неактивный" :
                    x.ClientStatus != null && x.ClientStatus < 0 ? "Удалён" : "Не указан"
            })
            .ToListAsync();

        var vm = new PatientsIndexViewModel
        {
            Search = search ?? string.Empty,
            Patients = patients
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Patients))
            return NotFound();

        return View(new CreatePatientViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePatientViewModel model)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Patients))
            return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
            return Unauthorized();

        var userId = AuthorizationHelper.GetUserId(HttpContext);
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return View(model);

        await _clientService.CreateClientAsync(
            new CreateClientInput
            {
                ClientName = model.Name.Trim(),
                ClientInn = string.IsNullOrWhiteSpace(model.Identifier) ? null : model.Identifier.Trim(),
                ClientPhone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                ClientEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                ClientAddress = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim()
            },
            tenant.OrganizationId,
            userId);

        TempData["Message"] = "Пациент добавлен.";
        return RedirectToAction(nameof(Index));
    }
}
