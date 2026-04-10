using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers;

[RequireAuth]
public class DepartmentsController : Controller
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(
        ICurrentTenantService currentTenantService,
        IDepartmentService departmentService)
    {
        _currentTenantService = currentTenantService;
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var items = await _departmentService.GetDepartmentsAsync(organizationId, search);
        ViewBag.Search = search?.Trim() ?? string.Empty;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        return View(new DepartmentUpsertDto { IsActive = true, SortOrder = 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentUpsertDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        if (!ModelState.IsValid)
            return View(dto);

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _departmentService.CreateAsync(organizationId, dto);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
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
        var dto = await _departmentService.GetDepartmentAsync(organizationId, id);
        if (dto == null)
            return NotFound();

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, DepartmentUpsertDto dto)
    {
        var gate = await EnsureAccessAsync();
        if (gate != null)
            return gate;

        if (!ModelState.IsValid)
            return View(dto);

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _departmentService.UpdateAsync(organizationId, id, dto);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
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

    public class DepartmentIndexItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public int SpecializationsCount { get; set; }
        public int DoctorsCount { get; set; }
    }
}
