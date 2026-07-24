using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class OrgClientGroupsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ICurrentTenantService _currentTenantService;

        public OrgClientGroupsController(AppDbContext db, ICurrentTenantService currentTenantService)
        {
            _db = db;
            _currentTenantService = currentTenantService;
        }

        private const int DefaultPageSize = 10;

        private IActionResult? EnsureOrgGroupsFeature()
        {
            var tenant = _currentTenantService.GetCurrent();
            return tenant.Profile.HasFeature(CabinetFeatures.OrgGroups) ? null : NotFound();
        }

        [RequirePermission("children.view")]
        public async Task<IActionResult> Index(string search = "", string isDeletedFilter = "active")
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

            var featureGuard = EnsureOrgGroupsFeature();
            if (featureGuard != null)
                return featureGuard;

            var query = _db.OrgClientGroups
                .Include(g => g.OrganizationClients)
                .Where(g => g.OrganizationId == organizationId);

            if (isDeletedFilter == "deleted")
                query = query.Where(g => g.IsDeleted == 1);
            else
                query = query.Where(g => g.IsDeleted == 0);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(g => g.Name != null && g.Name.ToLower().Contains(term));
            }

            var allGroups = await query.OrderBy(g => g.Name).ToListAsync();

            // Строим дерево: для каждой группы заполняем ChildGroups из плоского списка
            foreach (var g in allGroups)
            {
                g.ChildGroups = allGroups
                    .Where(c => c.ParentGroupId == g.Id)
                    .OrderBy(c => c.Name)
                    .ToList();
            }
            var roots = allGroups.Where(g => string.IsNullOrEmpty(g.ParentGroupId)).OrderBy(g => g.Name).ToList();

            ViewBag.Search = search?.Trim() ?? "";
            ViewBag.IsDeletedFilter = isDeletedFilter;
            ViewBag.TotalCount = allGroups.Count;

            var parentGroupsList = await _db.OrgClientGroups
                .Where(g => g.OrganizationId == organizationId && g.IsDeleted == 0)
                .OrderBy(g => g.Name)
                .Select(g => new { g.Id, g.Name })
                .ToListAsync();
            ViewBag.ParentGroupsForModal = parentGroupsList;
            ViewBag.CanManageChildren = AuthorizationHelper.CanManageChildren(HttpContext);

            return View(roots);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("children.create")]
        public async Task<IActionResult> Create(OrgClientGroup model)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var featureGuard = EnsureOrgGroupsFeature();
            if (featureGuard != null)
                return featureGuard;

            model.OrganizationId = organizationId;
            if (string.IsNullOrWhiteSpace(model.ParentGroupId))
                model.ParentGroupId = null;

            if (string.IsNullOrWhiteSpace(model.Name))
                return Json(new { success = false, message = "Укажите название группы." });

            model.Id = Guid.NewGuid().ToString();
            model.IsDeleted = 0;
            model.CreatedDate = DateTime.UtcNow;
            _db.OrgClientGroups.Add(model);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Группа создана." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("children.create")]
        public async Task<IActionResult> Edit(string id, OrgClientGroup model)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var featureGuard = EnsureOrgGroupsFeature();
            if (featureGuard != null)
                return featureGuard;

            if (id != model.Id)
                return Json(new { success = false, message = "Группа не найдена." });

            var group = await _db.OrgClientGroups
                .FirstOrDefaultAsync(g => g.Id == id && g.OrganizationId == organizationId);
            if (group == null)
                return Json(new { success = false, message = "Группа не найдена." });

            group.Name = model.Name;
            group.ParentGroupId = string.IsNullOrWhiteSpace(model.ParentGroupId) ? null : model.ParentGroupId;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Изменения сохранены." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("children.create")]
        public async Task<IActionResult> Delete(string id)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var featureGuard = EnsureOrgGroupsFeature();
            if (featureGuard != null)
                return featureGuard;

            var group = await _db.OrgClientGroups
                .Include(g => g.OrganizationClients)
                .Include(g => g.ChildGroups)
                .FirstOrDefaultAsync(g => g.Id == id && g.OrganizationId == organizationId);
            if (group == null)
                return Json(new { success = false, message = "Группа не найдена." });

            if (group.ChildGroups.Any())
                return Json(new { success = false, message = "Нельзя удалить группу, у которой есть дочерние группы. Сначала удалите или переназначьте дочерние группы." });

            foreach (var client in group.OrganizationClients.ToList())
                client.OrgClientGroupId = null;
            group.IsDeleted = 1;
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Группа удалена." });
        }

    }
}
