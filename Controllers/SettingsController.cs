using Microsoft.AspNetCore.Mvc;
using WebApplication1.Helpers;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class SettingsController : Controller
    {
        private readonly ICurrentTenantService _currentTenantService;

        public SettingsController(ICurrentTenantService currentTenantService)
        {
            _currentTenantService = currentTenantService;
        }

        private IActionResult? EnsureSettingsFeature()
        {
            var tenant = _currentTenantService.GetCurrent();
            return tenant.Profile.HasFeature(CabinetFeatures.Settings) ? null : NotFound();
        }

        public IActionResult Index()
        {
            var featureGuard = EnsureSettingsFeature();
            if (featureGuard != null)
                return featureGuard;

            return View();
        }
    }
}
