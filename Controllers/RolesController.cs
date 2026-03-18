using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [Helpers.RequireAuth]
    public class RolesController : Controller
    {
        private readonly AppDbContext _db;

        public RolesController(AppDbContext db)
        {
            _db = db;
        }

        private string? GetCurrentOrganizationId()
        {
            return HttpContext.Session.GetString("OrganizationId");
        }

        // GET: Roles
        public async Task<IActionResult> Index()
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Показываем только роли текущей организации
            var roles = await _db.Roles
                .Where(r => r.Organization == organizationId && 
                           (r.Isdeleted == null || r.Isdeleted == 0))
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.PermissionNavigation)
                .ToListAsync();

            return View(roles);
        }

        // GET: Roles/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var role = await _db.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.PermissionNavigation)
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        // GET: Roles/Create
        public async Task<IActionResult> Create()
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var organization = await _db.Organizations.FindAsync(organizationId);
            if (organization == null)
            {
                return NotFound();
            }

            ViewBag.Organization = organization;

            return View();
        }

        // POST: Roles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name")] Role role)
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Генерируем ID для роли до проверки ModelState
            var allRoleIds = await _db.Roles
                .Select(r => r.Id)
                .ToListAsync();

            int newRoleId = 1;
            foreach (var idStr in allRoleIds)
            {
                if (int.TryParse(idStr, out int id) && id >= newRoleId)
                {
                    newRoleId = id + 1;
                }
            }

            role.Id = newRoleId.ToString();
            role.Organization = organizationId; // Привязываем к организации
            role.Isdeleted = 0;

            // Удаляем ошибки валидации для полей, которые мы устанавливаем программно
            ModelState.Remove("Id");
            ModelState.Remove("Organization");
            ModelState.Remove("Isdeleted");

            if (ModelState.IsValid)
            {
                _db.Add(role);
                await _db.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            var organization = await _db.Organizations.FindAsync(organizationId);
            ViewBag.Organization = organization;

            return View(role);
        }

        // GET: Roles/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var role = await _db.Roles
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        // POST: Roles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,Name")] Role role)
        {
            if (id != role.Id)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Удаляем ошибки валидации для полей, которые мы не редактируем
            ModelState.Remove("Organization");
            ModelState.Remove("Isdeleted");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingRole = await _db.Roles
                        .FirstOrDefaultAsync(r => r.Id == id && r.Organization == organizationId);

                    if (existingRole == null)
                    {
                        return NotFound();
                    }

                    existingRole.Name = role.Name;
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoleExists(role.Id, organizationId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(role);
        }

        // GET: Roles/ManagePermissions/5
        public async Task<IActionResult> ManagePermissions(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var role = await _db.Roles
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (role == null)
            {
                return NotFound();
            }

            var organization = await _db.Organizations.FindAsync(organizationId);
            if (organization == null)
            {
                return NotFound();
            }

            // Получаем права: только для типа организации и общие (где Area = null)
            var organizationType = organization.Organizationtype;
            var permissions = await _db.Permissions
                .Where(p => (p.Isdeleted == null || p.Isdeleted == 0) &&
                           (p.Area == null || p.Area == organizationType))
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            ViewBag.Permissions = permissions;
            ViewBag.PermissionCategories = permissions
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Получаем выбранные права для роли
            var selectedPermissionIds = await _db.RolePermissions
                .Where(rp => rp.Role == id && 
                             (rp.Isdeleted == null || rp.Isdeleted == 0))
                .Select(rp => rp.Permission)
                .ToListAsync();

            ViewBag.SelectedPermissionIds = selectedPermissionIds;
            ViewBag.Organization = organization;

            return View(role);
        }

        // POST: Roles/ManagePermissions/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagePermissions(string id, List<string>? selectedPermissions)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var role = await _db.Roles
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (role == null)
            {
                return NotFound();
            }

            // Получаем все права для роли (включая удаленные) - используем AsNoTracking для избежания конфликтов
            var allRolePermissions = await _db.RolePermissions
                .Where(rp => rp.Role == id)
                .AsNoTracking()
                .ToListAsync();

            // Получаем ID всех существующих RolePermission для генерации нового ID
            var allRolePermissionIds = await _db.RolePermissions
                .Select(rp => rp.Id)
                .ToListAsync();

            int newRolePermissionId = 1;
            foreach (var idStr in allRolePermissionIds)
            {
                if (int.TryParse(idStr, out int rpId) && rpId >= newRolePermissionId)
                {
                    newRolePermissionId = rpId + 1;
                }
            }

            // Помечаем все существующие активные права как удаленные
            var activeRolePermissionIds = allRolePermissions
                .Where(rp => rp.Isdeleted == null || rp.Isdeleted == 0)
                .Select(rp => rp.Id)
                .ToList();

            foreach (var rpId in activeRolePermissionIds)
            {
                var trackedRp = await _db.RolePermissions
                    .FirstOrDefaultAsync(rp => rp.Id == rpId);
                if (trackedRp != null)
                {
                    trackedRp.Isdeleted = 1;
                }
            }

            // Обрабатываем выбранные права
            if (selectedPermissions != null && selectedPermissions.Any())
            {
                foreach (var permissionId in selectedPermissions)
                {
                    // Проверяем, существует ли уже такая связь (включая удаленные)
                    var existing = allRolePermissions
                        .FirstOrDefault(rp => rp.Permission == permissionId);

                    if (existing != null)
                    {
                        // Восстанавливаем существующую запись
                        var trackedRp = await _db.RolePermissions
                            .FirstOrDefaultAsync(rp => rp.Id == existing.Id);
                        if (trackedRp != null)
                        {
                            trackedRp.Isdeleted = 0;
                        }
                    }
                    else
                    {
                        // Создаем новую запись
                        var rolePermission = new RolePermission
                        {
                            Id = newRolePermissionId.ToString(),
                            Role = role.Id,
                            Permission = permissionId,
                            Isdeleted = 0
                        };

                        _db.RolePermissions.Add(rolePermission);
                        newRolePermissionId++; // Увеличиваем для следующей итерации
                    }
                }
            }

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(ManagePermissions), new { id = id });
        }

        // GET: Roles/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var role = await _db.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.PermissionNavigation)
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (role == null)
            {
                return NotFound();
            }

            return View(role);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var role = await _db.Roles
                .FirstOrDefaultAsync(r => r.Id == id && r.Organization == organizationId);

            if (role != null)
            {
                role.Isdeleted = 1;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool RoleExists(string id, string organizationId)
        {
            return (_db.Roles?.Any(e => e.Id == id && 
                                       e.Organization == organizationId &&
                                       (e.Isdeleted == null || e.Isdeleted == 0))).GetValueOrDefault();
        }
    }
}

