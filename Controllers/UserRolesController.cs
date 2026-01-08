using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [Helpers.RequireAuth]
    public class UserRolesController : Controller
    {
        private readonly AppDbContext _db;

        public UserRolesController(AppDbContext db)
        {
            _db = db;
        }

        // GET: UserRoles
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users
                .Where(u => u.Isdeleted == null || u.Isdeleted == 0)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.RoleNavigation)
                .OrderBy(u => u.Name)
                .ToListAsync();

            return View(users);
        }

        // GET: UserRoles/Assign/5
        public async Task<IActionResult> Assign(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.RoleNavigation)
                .FirstOrDefaultAsync(m => m.Id == id && (m.Isdeleted == null || m.Isdeleted == 0));

            if (user == null)
            {
                return NotFound();
            }

            var roles = await _db.Roles
                .Where(r => r.Isdeleted == null || r.Isdeleted == 0)
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.Roles = roles;
            ViewBag.SelectedRoleIds = user.UserRoles
                .Where(ur => ur.Isdeleted == null || ur.Isdeleted == 0)
                .Select(ur => ur.Role)
                .ToList();

            return View(user);
        }

        // POST: UserRoles/Assign/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string id, List<string>? selectedRoles)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == id && (u.Isdeleted == null || u.Isdeleted == 0));

            if (user == null)
            {
                return NotFound();
            }

            // Удаляем все существующие роли пользователя
            var existingUserRoles = user.UserRoles
                .Where(ur => ur.Isdeleted == null || ur.Isdeleted == 0)
                .ToList();

            foreach (var ur in existingUserRoles)
            {
                ur.Isdeleted = 1;
            }

            // Добавляем новые роли
            if (selectedRoles != null && selectedRoles.Any())
            {
                foreach (var roleId in selectedRoles)
                {
                    // Проверяем, не существует ли уже такая связь
                    var existing = user.UserRoles
                        .FirstOrDefault(ur => ur.Role == roleId && (ur.Isdeleted == null || ur.Isdeleted == 0));

                    if (existing != null)
                    {
                        existing.Isdeleted = 0; // Восстанавливаем, если была удалена
                    }
                    else
                    {
                        var allUserRoleIds = await _db.UserRoles
                            .Select(ur => ur.Id)
                            .ToListAsync();

                        int newUserRoleId = 1;
                        foreach (var idStr in allUserRoleIds)
                        {
                            if (int.TryParse(idStr, out int urId) && urId >= newUserRoleId)
                            {
                                newUserRoleId = urId + 1;
                            }
                        }

                        var userRole = new UserRole
                        {
                            Id = newUserRoleId.ToString(),
                            User = user.Id,
                            Role = roleId,
                            Isdeleted = 0
                        };

                        _db.UserRoles.Add(userRole);
                    }
                }
            }

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}

