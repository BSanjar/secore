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
            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Только пользователи текущей организации (как в UsersController)
            var users = await _db.Users
                .Where(u => u.Organization == organizationId && (u.Isdeleted == null || u.Isdeleted == 0))
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.RoleNavigation)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.CurrentOrganizationId = organizationId;
            return View(users);
        }

        // GET: UserRoles/Assign/5
        public async Task<IActionResult> Assign(string id)
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
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.RoleNavigation)
                .FirstOrDefaultAsync(m => m.Id == id
                    && m.Organization == organizationId
                    && (m.Isdeleted == null || m.Isdeleted == 0));

            if (user == null)
            {
                return NotFound();
            }

            var roles = await _db.Roles
                .Where(r => r.Organization == organizationId && (r.Isdeleted == null || r.Isdeleted == 0))
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.Roles = roles;
            // Только роли этой организации в списке выбранных (на случай устаревших связей в БД)
            var orgRoleIds = roles.Select(r => r.Id).ToHashSet();
            ViewBag.SelectedRoleIds = user.UserRoles
                .Where(ur => (ur.Isdeleted == null || ur.Isdeleted == 0) && ur.Role != null && orgRoleIds.Contains(ur.Role))
                .Select(ur => ur.Role!)
                .ToList();

            return View(user);
        }

        // POST: UserRoles/Assign/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(string id, List<string>? selectedRoles)
        {
            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var user = await _db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == id
                    && u.Organization == organizationId
                    && (u.Isdeleted == null || u.Isdeleted == 0));

            if (user == null)
            {
                return NotFound();
            }

            var validOrgRoleIds = await _db.Roles
                .Where(r => r.Organization == organizationId && (r.Isdeleted == null || r.Isdeleted == 0))
                .Select(r => r.Id)
                .ToListAsync();
            var validRoleSet = validOrgRoleIds.ToHashSet();

            // Снимаем только роли текущей организации (связи по чужим ролям не трогаем)
            var existingUserRoles = user.UserRoles
                .Where(ur => (ur.Isdeleted == null || ur.Isdeleted == 0) && ur.Role != null && validRoleSet.Contains(ur.Role))
                .ToList();

            foreach (var ur in existingUserRoles)
            {
                ur.Isdeleted = 1;
            }

            // Добавляем новые роли (только из списка ролей этой организации)
            if (selectedRoles != null && selectedRoles.Any())
            {
                foreach (var roleId in selectedRoles)
                {
                    if (string.IsNullOrEmpty(roleId) || !validRoleSet.Contains(roleId))
                    {
                        continue;
                    }

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

