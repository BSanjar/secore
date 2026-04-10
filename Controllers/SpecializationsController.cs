using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers;

[RequireAuth]
public class SpecializationsController : Controller
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IDepartmentService _departmentService;
    private readonly ISpecializationService _specializationService;

    public SpecializationsController(
        ICurrentTenantService currentTenantService,
        IDepartmentService departmentService,
        ISpecializationService specializationService)
    {
        _currentTenantService = currentTenantService;
        _departmentService = departmentService;
        _specializationService = specializationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var items = await _specializationService.GetSpecializationsAsync(organizationId, search);
        ViewBag.Search = search?.Trim() ?? string.Empty;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        ViewBag.Departments = await _specializationService.GetDepartmentOptionsAsync(organizationId);
        return View(new SpecializationUpsertDto { IsActive = true, SortOrder = 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SpecializationUpsertDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        if (!ModelState.IsValid)
        {
            ViewBag.Departments = await _specializationService.GetDepartmentOptionsAsync(organizationId, dto.DepartmentId);
            return View(dto);
        }

        var result = await _specializationService.CreateAsync(organizationId, dto);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            ViewBag.Departments = await _specializationService.GetDepartmentOptionsAsync(organizationId, dto.DepartmentId);
            return View(dto);
        }

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var dto = await _specializationService.GetSpecializationAsync(organizationId, id);
        if (dto == null)
            return NotFound();

        ViewBag.Departments = await _specializationService.GetDepartmentOptionsAsync(organizationId, dto.DepartmentId);
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SpecializationUpsertDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        if (!ModelState.IsValid)
        {
            ViewBag.Departments = await _specializationService.GetDepartmentOptionsAsync(organizationId, dto.DepartmentId);
            return View(dto);
        }

        var result = await _specializationService.UpdateAsync(organizationId, id, dto);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            ViewBag.Departments = await _specializationService.GetDepartmentOptionsAsync(organizationId, dto.DepartmentId);
            return View(dto);
        }

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult?> EnsureAccessAsync()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.MedicalStructure))
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

    public class SpecializationIndexItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public int DoctorsCount { get; set; }
    }
}
