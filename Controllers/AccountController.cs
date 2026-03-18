using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Dtos;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db)
        {
            _db = db;
        }

        // GET: Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            // Если пользователь уже авторизован, перенаправляем в личный кабинет
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            var organizationType = HttpContext.Session.GetString("OrganizationType");

            if (!string.IsNullOrEmpty(organizationId) && !string.IsNullOrEmpty(organizationType))
            {
                var area = GetAreaByOrganizationType(organizationType);
                return RedirectToAction("Index", "Cabinet", new { area = area });
            }

            ViewData["ReturnUrl"] = returnUrl;
            var model = new LoginViewModel();
            return View(model);
        }

        // POST: Account/Login
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

            // Сначала ищем по точному совпадению без учёта регистра в БД (индекс по LOWER(email))
            var user = await _db.Users
                .Include(u => u.OrganizationNavigation)
                .Where(u => (u.Isdeleted == null || u.Isdeleted == 0)
                    && u.Email != null
                    && u.Email.ToLower() == emailToSearch.ToLower())
                .FirstOrDefaultAsync();

            // Если не найден — возможны пробелы в начале/конце в БД: ищем среди ограниченного набора
            if (user == null)
            {
                var candidates = await _db.Users
                    .Include(u => u.OrganizationNavigation)
                    .Where(u => (u.Isdeleted == null || u.Isdeleted == 0)
                        && u.Email != null
                        && u.Email.ToLower().Contains(emailToSearch.ToLower())
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

            // Проверяем пароль
            if (string.IsNullOrEmpty(user.Password) || 
                !PasswordHelper.VerifyPassword(model.Password, user.Password))
            {
                ModelState.AddModelError("", "Неверный email или пароль");
                return View(model);
            }

            // Проверяем, что у пользователя есть организация
            if (string.IsNullOrEmpty(user.Organization) || user.OrganizationNavigation == null)
            {
                ModelState.AddModelError("", "Пользователь не привязан к организации");
                return View(model);
            }

            // Проверка доступа: только по флагу активности организации (is_active)
            if (!await SubscriptionHelper.HasSubscriptionAccessAsync(_db, user.Organization))
            {
                ModelState.AddModelError("", "Доступ приостановлен: организация неактивна. Обратитесь к администратору.");
                return View(model);
            }

            // Сохраняем данные в сессии
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "Пользователь");
            HttpContext.Session.SetString("OrganizationId", user.Organization);
            HttpContext.Session.SetString("OrganizationType", user.OrganizationNavigation.Organizationtype ?? "");

            // Определяем Area для перенаправления на основе типа организации
            var area = GetAreaByOrganizationType(user.OrganizationNavigation.Organizationtype);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Перенаправляем в соответствующий личный кабинет
            return RedirectToAction("Index", "Cabinet", new { area = area });
        }

        /// <summary>
        /// Страница «Подписка истекла» — показывается при редиректе из RequireAuth, когда у организации нет действующего оплаченного периода.
        /// </summary>
        public IActionResult SubscriptionExpired()
        {
            HttpContext.Session.Clear();
            return View();
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // GET: Account/Logout (для удобства)
        public IActionResult LogoutGet()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        /// <summary>
        /// Определяет Area на основе типа организации
        /// </summary>
        private string GetAreaByOrganizationType(string? organizationType)
        {
            return organizationType?.ToLower() switch
            {
                "detsad" => "Detsad",
                "school" => "School",
                "medclinic" => "Medclinic",
                "standart" => "Standart",
                "simple" => "Simple",
                _ => "Standart" // По умолчанию
            };
        }
    }
}
