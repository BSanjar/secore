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

    public DoctorsController(
        ICurrentTenantService currentTenantService,
        IDepartmentService departmentService,
        IDoctorDirectoryService doctorDirectoryService)
    {
        _currentTenantService = currentTenantService;
        _departmentService = departmentService;
        _doctorDirectoryService = doctorDirectoryService;
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
            Doctors = doctors
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
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
    public async Task<IActionResult> Edit(string id, DoctorAssignmentsUpsertDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

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
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchedule(string id, SaveUserWorkScheduleRequest dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        dto.UserId = id;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _doctorDirectoryService.UpdateDoctorScheduleAsync(organizationId, dto);

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Edit), new { id });
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

        dto.UserId = id;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _doctorDirectoryService.UpdateDoctorScheduleOverrideAsync(organizationId, dto);

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Edit), new { id });
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
