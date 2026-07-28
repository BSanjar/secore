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
    private readonly NotificationRecipientResolver _recipientResolver;
    private readonly ExcelExportService _excelExportService;

    public PatientsController(
        AppDbContext db,
        ClientService clientService,
        ICurrentTenantService currentTenantService,
        NotificationRecipientResolver recipientResolver,
        ExcelExportService excelExportService)
    {
        _db = db;
        _clientService = clientService;
        _currentTenantService = currentTenantService;
        _recipientResolver = recipientResolver;
        _excelExportService = excelExportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Patients))
            return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
            return Unauthorized();

        var patients = await LoadPatientsAsync(tenant.OrganizationId, search);

        ViewBag.CanViewAppointmentRegistry = PermissionHelper.HasPermission(HttpContext, "appointments.registry.view")
            || PermissionHelper.HasPermission(HttpContext, "appointments.view");

        var vm = new PatientsIndexViewModel
        {
            Search = search ?? string.Empty,
            Patients = patients
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? search)
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Patients))
            return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
            return Unauthorized();

        var patients = await LoadPatientsAsync(tenant.OrganizationId, search);

        var rows = patients.Select(p => (IReadOnlyList<object?>)new List<object?>
        {
            string.IsNullOrWhiteSpace(p.Address) ? p.Name : $"{p.Name} ({p.Address})",
            string.IsNullOrWhiteSpace(p.Gender) ? "—" : p.Gender,
            FormatPatientBirthDate(p),
            string.IsNullOrWhiteSpace(p.Identifier) ? "—" : p.Identifier,
            string.IsNullOrWhiteSpace(p.Phone) ? "—" : p.Phone,
            string.IsNullOrWhiteSpace(p.Email) ? "—" : p.Email,
            p.AppointmentsCount,
            p.StatusLabel,
            p.CreatedDate
        }).ToList();

        var bytes = _excelExportService.ExportTable(
            "Пациенты",
            new[] { "Пациент", "Пол", "Дата рождения", "ИИН/ИНН", "Телефон", "Email", "Записей", "Статус", "Создан" },
            rows);

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"pacienty_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");
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

    private async Task<List<PatientListItemViewModel>> LoadPatientsAsync(string organizationId, string? search)
    {
        var query = _db.OrganizationClients
            .AsNoTracking()
            .Where(x => x.Organization == organizationId && (x.ClientStatus == null || x.ClientStatus >= 0));

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

        var patientIds = patients.Select(x => x.Id).ToList();
        if (patientIds.Count > 0)
        {
            var appointmentCounts = await _db.Appointments
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.PatientId != null &&
                    patientIds.Contains(x.PatientId) &&
                    x.IsActive &&
                    (x.AppointmentStatus == null || x.AppointmentStatus.ToLower() != "cancelled"))
                .GroupBy(x => x.PatientId!)
                .Select(g => new { PatientId = g.Key, Count = g.Count() })
                .ToListAsync();

            var countMap = appointmentCounts.ToDictionary(x => x.PatientId, x => x.Count);
            foreach (var patient in patients)
            {
                if (countMap.TryGetValue(patient.Id, out var count))
                    patient.AppointmentsCount = count;
            }
        }

        if (patients.Count > 0)
        {
            var demographics = await _recipientResolver.LoadDemographicsAsync(
                organizationId,
                patients.Select(x => x.Id),
                CancellationToken.None);

            foreach (var patient in patients)
            {
                if (!demographics.TryGetValue(patient.Id, out var demo))
                    continue;

                patient.Gender = NotificationRecipientResolver.FormatGenderDisplay(demo.Gender, demo.GenderRaw);
                patient.BirthDate = demo.BirthDate;
                patient.Age = demo.Age;
            }
        }

        return patients;
    }

    private static string FormatPatientBirthDate(PatientListItemViewModel patient)
    {
        if (string.IsNullOrWhiteSpace(patient.BirthDate))
            return "—";

        if (DateTime.TryParse(patient.BirthDate, out var birthDate))
        {
            return patient.Age is not null
                ? $"{birthDate:dd.MM.yyyy} ({patient.Age} лет)"
                : birthDate.ToString("dd.MM.yyyy");
        }

        return patient.BirthDate;
    }
}
