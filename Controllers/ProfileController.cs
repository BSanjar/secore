using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ICurrentTenantService _currentTenantService;

        public ProfileController(AppDbContext db, ICurrentTenantService currentTenantService)
        {
            _db = db;
            _currentTenantService = currentTenantService;
        }

        private IActionResult? EnsureProfileFeature()
        {
            var tenant = _currentTenantService.GetCurrent();
            return tenant.Profile.HasFeature(CabinetFeatures.Profile) ? null : NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = AuthorizationHelper.GetUserId(HttpContext);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var featureGuard = EnsureProfileFeature();
            if (featureGuard != null)
                return featureGuard;

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string? Name, string? Phone, string? StartPage)
        {
            var userId = AuthorizationHelper.GetUserId(HttpContext);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var featureGuard = EnsureProfileFeature();
            if (featureGuard != null)
                return featureGuard;

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.Name = Name?.Trim();
            user.Phone = Phone?.Trim();
            user.StartPage = LandingPageResolver.NormalizeStartPage(StartPage);
            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("UserName", user.Name ?? "");

            TempData["Message"] = "Профиль сохранён.";
            return RedirectToAction(nameof(Index));
        }
    }
}
