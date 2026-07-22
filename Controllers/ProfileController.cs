using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Profile;

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

            var user = await _db.Users
                .AsNoTracking()
                .Include(u => u.OrganizationNavigation)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.RoleNavigation)
                .FirstOrDefaultAsync(u => u.Id == userId && (u.Isdeleted == null || u.Isdeleted == 0));

            if (user == null)
                return NotFound();

            var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
            var selectedStartPage = LandingPageResolver.NormalizeStartPage(user.StartPage);

            var roles = user.UserRoles
                .Where(ur => ur.Isdeleted == null || ur.Isdeleted == 0)
                .Select(ur => ur.RoleNavigation?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();

            var model = new ProfileViewModel
            {
                Name = string.IsNullOrWhiteSpace(user.Name) ? "Пользователь" : user.Name.Trim(),
                Email = user.Email,
                Phone = user.Phone,
                OrganizationName = user.OrganizationNavigation?.Name,
                OrganizationType = organizationType,
                StartPageLabel = selectedStartPage,
                LastLogin = user.LastLogin,
                Roles = roles,
                Initials = BuildInitials(user.Name)
            };

            return View(model);
        }

        private static string BuildInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "?";

            var parts = name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
                return "?";

            if (parts.Length == 1)
                return parts[0][..1].ToUpperInvariant();

            return string.Concat(parts[0].AsSpan(0, 1), parts[^1].AsSpan(0, 1)).ToUpperInvariant();
        }
    }
}
