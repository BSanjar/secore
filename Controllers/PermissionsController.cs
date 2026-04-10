using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [Helpers.RequireAuth]
    public class PermissionsController : Controller
    {
        private readonly AppDbContext _db;

        public PermissionsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: Permissions
        public async Task<IActionResult> Index()
        {
            var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
            
            // Показываем только права для типа организации и общие (где Area = null)
            var permissions = await _db.Permissions
                .Where(p => (p.Isdeleted == null || p.Isdeleted == 0) &&
                           (p.Area == null || p.Area == organizationType))
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return View(permissions);
        }

        // GET: Permissions/Create
        public async Task<IActionResult> Create()
        {
            var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
            ViewBag.OrganizationType = organizationType;
            ViewBag.Areas = new List<string> { "standart", "detsad", "school", "medclinic", "simple" };
            return View();
        }

        // POST: Permissions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Code,Description,Category,Area")] Permission permission)
        {
            if (ModelState.IsValid)
            {
                // Генерируем ID для права
                var allPermissionIds = await _db.Permissions
                    .Select(p => p.Id)
                    .ToListAsync();

                int newPermissionId = 1;
                foreach (var idStr in allPermissionIds)
                {
                    if (int.TryParse(idStr, out int id) && id >= newPermissionId)
                    {
                        newPermissionId = id + 1;
                    }
                }

                permission.Id = newPermissionId.ToString();
                permission.Isdeleted = 0;

                _db.Add(permission);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
            ViewBag.OrganizationType = organizationType;
            ViewBag.Areas = new List<string> { "standart", "detsad", "school", "medclinic", "simple" };
            return View(permission);
        }

        // GET: Permissions/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _db.Permissions
                .FirstOrDefaultAsync(m => m.Id == id && (m.Isdeleted == null || m.Isdeleted == 0));

            if (permission == null)
            {
                return NotFound();
            }

            var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
            ViewBag.OrganizationType = organizationType;
            ViewBag.Areas = new List<string> { "standart", "detsad", "school", "medclinic", "simple" };

            return View(permission);
        }

        // POST: Permissions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,Name,Code,Description,Category,Area")] Permission permission)
        {
            if (id != permission.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingPermission = await _db.Permissions.FindAsync(id);
                    if (existingPermission == null)
                    {
                        return NotFound();
                    }

                    existingPermission.Name = permission.Name;
                    existingPermission.Code = permission.Code;
                    existingPermission.Description = permission.Description;
                    existingPermission.Category = permission.Category;
                    existingPermission.Area = permission.Area;

                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PermissionExists(permission.Id))
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

            var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
            ViewBag.OrganizationType = organizationType;
            ViewBag.Areas = new List<string> { "standart", "detsad", "school", "medclinic", "simple" };
            return View(permission);
        }

        // GET: Permissions/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var permission = await _db.Permissions
                .FirstOrDefaultAsync(m => m.Id == id && (m.Isdeleted == null || m.Isdeleted == 0));

            if (permission == null)
            {
                return NotFound();
            }

            return View(permission);
        }

        // POST: Permissions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var permission = await _db.Permissions.FindAsync(id);
            if (permission != null)
            {
                permission.Isdeleted = 1;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PermissionExists(string id)
        {
            return (_db.Permissions?.Any(e => e.Id == id && (e.Isdeleted == null || e.Isdeleted == 0))).GetValueOrDefault();
        }
    }
}

