using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string? Name, string? Phone)
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

            TempData["Message"] = "Профиль сохранён.";
            return RedirectToAction(nameof(Index));
        }
    }
}
