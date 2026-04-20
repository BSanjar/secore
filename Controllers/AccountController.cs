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

        public AccountController(AppDbContext db)
        {
            _db = db;
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
                ModelState.AddModelError("", "Неверный email или пароль");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.Password) ||
                !PasswordHelper.VerifyPassword(model.Password, user.Password))
            {
                ModelState.AddModelError("", "Неверный email или пароль");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.Organization) || user.OrganizationNavigation == null)
            {
                ModelState.AddModelError("", "Пользователь не привязан к организации");
                return View(model);
            }

            if (!await SubscriptionHelper.HasSubscriptionAccessAsync(_db, user.Organization))
            {
                ModelState.AddModelError("", "Доступ приостановлен: организация неактивна. Обратитесь к администратору.");
                return View(model);
            }

            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "Пользователь");
            HttpContext.Session.SetString("OrganizationId", user.Organization);
            HttpContext.Session.SetString("OrganizationType", user.OrganizationNavigation.Organizationtype ?? "");

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
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult LogoutGet()
        {
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
