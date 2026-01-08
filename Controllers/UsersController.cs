using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class UsersController : Controller
    {
        private readonly AppDbContext _db;

        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var users = await _db.Users
                .Where(u => u.Organization == organizationId && 
                           (u.Isdeleted == null || u.Isdeleted == 0))
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.RoleNavigation)
                .OrderBy(u => u.Name)
                .ToListAsync();

            return View(users);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Email,Phone,Password")] User user)
        {
            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Проверяем, не существует ли уже пользователь с таким email
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email && 
                                         (u.Isdeleted == null || u.Isdeleted == 0));

            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
            }

            // Генерируем ID для пользователя
            var allUserIds = await _db.Users
                .Select(u => u.Id)
                .ToListAsync();

            int newUserId = 1;
            foreach (var idStr in allUserIds)
            {
                if (int.TryParse(idStr, out int id) && id >= newUserId)
                {
                    newUserId = id + 1;
                }
            }

            user.Id = newUserId.ToString();
            user.Organization = organizationId;
            user.Isdeleted = 0;

            // Хешируем пароль
            if (!string.IsNullOrEmpty(user.Password))
            {
                user.Password = PasswordHelper.HashPassword(user.Password);
            }

            // Удаляем Id, Organization, Isdeleted из ModelState
            ModelState.Remove("Id");
            ModelState.Remove("Organization");
            ModelState.Remove("Isdeleted");
            ModelState.Remove("Password"); // Пароль уже обработан

            if (ModelState.IsValid)
            {
                _db.Add(user);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Пользователь успешно создан";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (user == null)
            {
                return NotFound();
            }

            // Не передаем пароль в представление
            user.Password = null;

            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,Name,Email,Phone,Password")] User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == id && 
                                         u.Organization == organizationId &&
                                         (u.Isdeleted == null || u.Isdeleted == 0));

            if (existingUser == null)
            {
                return NotFound();
            }

            // Проверяем, не используется ли email другим пользователем
            var emailUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email && 
                                         u.Id != id &&
                                         (u.Isdeleted == null || u.Isdeleted == 0));

            if (emailUser != null)
            {
                ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
            }

            // Обновляем данные
            existingUser.Name = user.Name;
            existingUser.Email = user.Email;
            existingUser.Phone = user.Phone;

            // Обновляем пароль только если он указан
            if (!string.IsNullOrEmpty(user.Password))
            {
                existingUser.Password = PasswordHelper.HashPassword(user.Password);
            }

            // Удаляем из ModelState
            ModelState.Remove("Organization");
            ModelState.Remove("Isdeleted");

            if (ModelState.IsValid)
            {
                try
                {
                    await _db.SaveChangesAsync();
                    TempData["Success"] = "Пользователь успешно обновлен";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(user);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/DeleteConfirmed/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (user != null)
            {
                user.Isdeleted = 1;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Пользователь успешно удален";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(string id)
        {
            return (_db.Users?.Any(e => e.Id == id && (e.Isdeleted == null || e.Isdeleted == 0))).GetValueOrDefault();
        }
    }
}

