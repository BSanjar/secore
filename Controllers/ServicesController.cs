using Microsoft.AspNetCore.Mvc;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Services;

namespace WebApplication1.Controllers;

[RequireAuth]
public class ServicesController : Controller
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IServiceCatalogService _serviceCatalogService;

    public ServicesController(
        ICurrentTenantService currentTenantService,
        IServiceCatalogService serviceCatalogService)
    {
        _currentTenantService = currentTenantService;
        _serviceCatalogService = serviceCatalogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var gate = EnsureAccess();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var services = await _serviceCatalogService.GetServicesAsync(organizationId, search);

        return View(new ServicesIndexViewModel
        {
            Search = search?.Trim(),
            Services = services
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        var gate = EnsureAccess();
        if (gate != null)
            return gate;

        return View(new ServiceUpsertDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceUpsertDto dto)
    {
        var gate = EnsureAccess();
        if (gate != null)
            return gate;

        if (!ModelState.IsValid)
            return View(dto);

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _serviceCatalogService.CreateAsync(organizationId, dto);
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
        var gate = EnsureAccess();
        if (gate != null)
            return gate;

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var dto = await _serviceCatalogService.GetServiceAsync(organizationId, id);
        if (dto == null)
            return NotFound();

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ServiceUpsertDto dto)
    {
        var gate = EnsureAccess();
        if (gate != null)
            return gate;

        if (!ModelState.IsValid)
            return View(dto);

        var organizationId = _currentTenantService.GetCurrent().OrganizationId!;
        var result = await _serviceCatalogService.UpdateAsync(organizationId, id, dto);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(dto);
        }

        TempData["Message"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private IActionResult? EnsureAccess()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (!tenant.Profile.HasFeature(CabinetFeatures.OrgServices))
            return NotFound();

        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
            return Unauthorized();

        return null;
    }
}
