using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
    [RequireAuth]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;

        public ProfileController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = AuthorizationHelper.GetUserId(HttpContext);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var cultureName = requestCulture?.RequestCulture?.Culture?.Name ?? "ru";

            ViewBag.CultureName = cultureName;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string? Name, string? Phone, string? Culture)
        {
            var userId = AuthorizationHelper.GetUserId(HttpContext);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.Name = Name?.Trim();
            user.Phone = Phone?.Trim();
            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("UserName", user.Name ?? "");

            var culture = string.IsNullOrEmpty(Culture) ? "ru" : Culture;
            var supportedCultures = new[] { "ru", "en", "ky" };
            if (!supportedCultures.Contains(culture))
                culture = "ru";

            var requestCulture = new RequestCulture(culture, culture);
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(requestCulture),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });

            TempData["Message"] = "Профиль сохранён.";
            return RedirectToAction(nameof(Index));
        }
    }
}
