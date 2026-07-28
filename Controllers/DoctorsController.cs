using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers;

[RequireAuth]
public class DoctorsController : Controller
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IDepartmentService _departmentService;
    private readonly IDoctorDirectoryService _doctorDirectoryService;
    private readonly ExcelExportService _excelExportService;

    public DoctorsController(
        ICurrentTenantService currentTenantService,
        IDepartmentService departmentService,
        IDoctorDirectoryService doctorDirectoryService,
        ExcelExportService excelExportService)
    {
        _currentTenantService = currentTenantService;
        _departmentService = departmentService;
        _doctorDirectoryService = doctorDirectoryService;
        _excelExportService = excelExportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var doctors = await _doctorDirectoryService.GetDoctorsAsync(organizationId, search);

        return View(new ViewModels.Doctors.DoctorsIndexViewModel
        {
            Search = search?.Trim(),
            CanManageStaff = AuthorizationHelper.CanManageStaff(HttpContext),
            Doctors = doctors
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? search)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var doctors = await _doctorDirectoryService.GetDoctorsAsync(organizationId, search);

        var rows = doctors.Select(d => (IReadOnlyList<object?>)new List<object?>
        {
            d.Name,
            d.Email ?? "—",
            d.Phone ?? "—",
            d.Roles.Count == 0 ? "—" : string.Join(", ", d.Roles),
            d.Departments.Count == 0 ? "—" : string.Join(", ", d.Departments),
            d.Specializations.Count == 0 ? "—" : string.Join(", ", d.Specializations)
        }).ToList();

        var bytes = _excelExportService.ExportTable(
            "Сотрудники",
            new[] { "Сотрудник", "Email", "Телефон", "Роли", "Отделения", "Специализации" },
            rows);

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sotrudniki_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        return RedirectToAction("Edit", "Users", new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Appointments(string id)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var model = await _doctorDirectoryService.GetDoctorAssignmentsAsync(organizationId, id);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStaffCard(string id, [Bind(Prefix = "Form")] StaffCardSaveDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var manageGate = EnsureManageStaffAccess();
        if (manageGate != null)
            return manageGate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;

        dto.Id = id;
        dto.SelectedRoleIds = Request.Form["SelectedRoleIds"].ToList().Where(x => x != null).Select(x => x!).ToList();
        dto.SelectedDepartmentIds = Request.Form["SelectedDepartmentIds"].ToList().Where(x => x != null).Select(x => x!).ToList();
        dto.SelectedSpecializationIds = Request.Form["SelectedSpecializationIds"].ToList().Where(x => x != null).Select(x => x!).ToList();
        dto.PrimaryDepartmentId = Request.Form["PrimaryDepartmentId"].FirstOrDefault();
        dto.PrimarySpecializationId = Request.Form["PrimarySpecializationId"].FirstOrDefault();
        dto.DaysOfWeek = Request.Form["DaysOfWeek"]
            .Select(x => int.TryParse(x, out var day) ? day : 0)
            .Where(x => x is >= 1 and <= 7)
            .ToList();

        if (string.IsNullOrWhiteSpace(dto.Password))
            ModelState.Remove("Form.Password");

        TryValidateModel(dto);

        if (!ModelState.IsValid)
        {
            var model = await _doctorDirectoryService.GetStaffCardAsync(organizationId, id);
            if (model == null)
                return NotFound();

            model.Form = dto;
            model.CanEdit = true;
            return View("~/Views/Users/EditMedclinic.cshtml", model);
        }

        var result = await _doctorDirectoryService.UpdateStaffCardAsync(organizationId, dto);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            var model = await _doctorDirectoryService.GetStaffCardAsync(organizationId, id);
            if (model == null)
                return NotFound();

            model.Form = dto;
            model.CanEdit = true;
            return View("~/Views/Users/EditMedclinic.cshtml", model);
        }

        TempData["Message"] = result.Message;
        return RedirectToAction("Edit", "Users", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, DoctorAssignmentsUpsertDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var manageGate = EnsureManageStaffAccess();
        if (manageGate != null)
            return manageGate;

        if (id != dto.Id)
            return NotFound();

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _doctorDirectoryService.UpdateDoctorAssignmentsAsync(organizationId, dto);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            var model = await _doctorDirectoryService.GetDoctorAssignmentsAsync(organizationId, id);
            if (model == null)
                return NotFound();
            model.Form = dto;
            return View(model);
        }

        TempData["Message"] = result.Message;
        return RedirectToAction("Edit", "Users", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchedule(string id, SaveUserWorkScheduleRequest dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var manageGate = EnsureManageStaffAccess();
        if (manageGate != null)
            return manageGate;

        dto.UserId = id;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _doctorDirectoryService.UpdateDoctorScheduleAsync(organizationId, dto);

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Edit", "Users", new { id });
        }

        TempData["Message"] = result.Message;
        return RedirectToAction("Edit", "Users", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAppointmentDuration(string id, int appointmentDurationMinutes)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var manageGate = EnsureManageStaffAccess();
        if (manageGate != null)
            return manageGate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _doctorDirectoryService.UpdateAppointmentDurationAsync(organizationId, id, appointmentDurationMinutes);

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Edit", "Users", new { id });
        }

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveScheduleOverride(string id, SaveUserWorkScheduleOverrideRequest dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var manageGate = EnsureManageStaffAccess();
        if (manageGate != null)
            return manageGate;

        dto.UserId = id;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _doctorDirectoryService.UpdateDoctorScheduleOverrideAsync(organizationId, dto);

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Edit", "Users", new { id });
        }

        TempData["Message"] = result.Message;
        return RedirectToAction("Edit", "Users", new { id });
    }

    private IActionResult? EnsureManageStaffAccess()
    {
        if (AuthorizationHelper.CanManageStaff(HttpContext))
            return null;

        return RedirectToAction("AccessDenied", "Home", new { permissionCode = "doctors.edit" });
    }

    private async Task<IActionResult?> EnsureAccessAsync()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.Doctors))
            return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
            return Unauthorized();

        if (!await _departmentService.StorageReadyAsync())
        {
            TempData["Error"] = "Таблицы отделений и специализаций еще не созданы. Сначала примените SQL-скрипт структуры.";
            return RedirectToAction(nameof(Index));
        }

        return null;
    }
}
