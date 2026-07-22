using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AppDbContext db, ILogger<AccountController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public IActionResult Login(string? returnUrl = null)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            var organizationType = HttpContext.Session.GetString("OrganizationType");
            var userId = HttpContext.Session.GetString("UserId");

            if (!string.IsNullOrEmpty(organizationId) && !string.IsNullOrEmpty(organizationType) && !string.IsNullOrEmpty(userId))
            {
                var user = _db.Users.AsNoTracking().FirstOrDefault(x => x.Id == userId);
                return RedirectToLanding(user, returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var emailToSearch = model.Email?.Trim();
            if (string.IsNullOrWhiteSpace(emailToSearch))
            {
                ModelState.AddModelError("", "Email не может быть пустым");
                return View(model);
            }

            var normalizedEmail = emailToSearch.ToLower();
            var user = await _db.Users
                .Include(u => u.OrganizationNavigation)
                .Where(u => (u.Isdeleted == null || u.Isdeleted == 0)
                    && u.Email != null
                    && u.Email.ToLower() == normalizedEmail)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                var candidates = await _db.Users
                    .Include(u => u.OrganizationNavigation)
                    .Where(u => (u.Isdeleted == null || u.Isdeleted == 0)
                        && u.Email != null
                        && u.Email.ToLower().Contains(normalizedEmail)
                        && u.Email.Length <= emailToSearch.Length + 10)
                    .ToListAsync();

                user = candidates.FirstOrDefault(u =>
                    string.Equals(u.Email?.Trim(), emailToSearch, StringComparison.OrdinalIgnoreCase));
            }

            if (user == null)
            {
                _logger.LogWarning("Login failed: user not found. Email={Email} IP={Ip}",
                    emailToSearch, HttpContext.Connection.RemoteIpAddress);
                ModelState.AddModelError("", "Неверный email или пароль");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.Password) ||
                !PasswordHelper.VerifyPassword(model.Password, user.Password))
            {
                _logger.LogWarning("Login failed: bad password. UserId={UserId} Email={Email} IP={Ip}",
                    user.Id, emailToSearch, HttpContext.Connection.RemoteIpAddress);
                ModelState.AddModelError("", "Неверный email или пароль");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.Organization) || user.OrganizationNavigation == null)
            {
                _logger.LogWarning("Login failed: no organization. UserId={UserId}", user.Id);
                ModelState.AddModelError("", "Пользователь не привязан к организации");
                return View(model);
            }

            if (!await SubscriptionHelper.HasSubscriptionAccessAsync(_db, user.Organization))
            {
                _logger.LogWarning("Login failed: subscription blocked. UserId={UserId} Org={OrgId}",
                    user.Id, user.Organization);
                ModelState.AddModelError("", "Доступ приостановлен: организация неактивна. Обратитесь к администратору.");
                return View(model);
            }

            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "Пользователь");
            HttpContext.Session.SetString("OrganizationId", user.Organization);
            HttpContext.Session.SetString("OrganizationType", user.OrganizationNavigation.Organizationtype ?? "");

            _logger.LogInformation(
                "Login success. UserId={UserId} Email={Email} Org={OrgId} OrgType={OrgType} IP={Ip}",
                user.Id,
                emailToSearch,
                user.Organization,
                user.OrganizationNavigation.Organizationtype,
                HttpContext.Connection.RemoteIpAddress);

            return RedirectToLanding(user, returnUrl);
        }

        public IActionResult SubscriptionExpired()
        {
            HttpContext.Session.Clear();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var orgId = HttpContext.Session.GetString("OrganizationId");
            _logger.LogInformation("Logout. UserId={UserId} Org={OrgId}", userId, orgId);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult LogoutGet()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var orgId = HttpContext.Session.GetString("OrganizationId");
            _logger.LogInformation("Logout (GET). UserId={UserId} Org={OrgId}", userId, orgId);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        private IActionResult RedirectToLanding(User? user, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var route = LandingPageResolver.ResolveRoute(HttpContext, user);
            return route == LandingPageResolver.Appointments
                ? RedirectToAction("Index", "Appointments")
                : RedirectToAction("Index", "Cabinet");
        }
    }
}
