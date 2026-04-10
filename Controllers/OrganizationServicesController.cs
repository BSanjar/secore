using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class OrganizationServicesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ICurrentTenantService _currentTenantService;

        public OrganizationServicesController(AppDbContext db, ICurrentTenantService currentTenantService)
        {
            _db = db;
            _currentTenantService = currentTenantService;
        }

        private string? GetOrganizationId()
        {
            return HttpContext.Session.GetString("OrganizationId");
        }

        private IActionResult? EnsureOrgServicesFeature()
        {
            var tenant = _currentTenantService.GetCurrent();
            return tenant.Profile.HasFeature(CabinetFeatures.OrgServices) ? null : NotFound();
        }

        /// <summary>Запрос из модального окна на Index (fetch + JSON), как в OrgClientGroups.</summary>
        private static bool IsFormModal(HttpRequest request) =>
            string.Equals(request.Headers["X-Form-Modal"].ToString(), "1", StringComparison.Ordinal);

        private static string FirstModelError(ModelStateDictionary modelState) =>
            modelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?? "Проверьте введённые данные.";

        [RequirePermission("children.view")]
        public async Task<IActionResult> Index(string search = "", string isDeletedFilter = "active")
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

            var featureGuard = EnsureOrgServicesFeature();
            if (featureGuard != null)
                return featureGuard;

            var query = _db.OrganizationServices
                .Where(s => s.Organization == organizationId);

            if (isDeletedFilter == "deleted")
                query = query.Where(s => s.Isdeleted == 1);
            else
                query = query.Where(s => s.Isdeleted != 1);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s => s.Name != null && s.Name.ToLower().Contains(term));
            }

            var list = await query.OrderBy(s => s.Name).ToListAsync();

            ViewBag.Search = search?.Trim() ?? "";
            ViewBag.IsDeletedFilter = isDeletedFilter;
            return View(list);
        }

        [RequirePermission("children.create")]
        [HttpGet]
        public IActionResult Create()
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

            var featureGuard = EnsureOrgServicesFeature();
            if (featureGuard != null)
                return featureGuard;

            return View(new OrganizationService { Organization = organizationId, FixedSum = 0, Isdeleted = 0 });
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrganizationService model)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = "Не авторизован." });
                return RedirectToAction("Login", "Account");
            }

            var featureGuard = EnsureOrgServicesFeature();
            if (featureGuard != null)
                return featureGuard;

            // Id не приходит из формы при создании; задаём Guid при сохранении. Иначе валидация: "The Id field is required."
            ModelState.Remove(nameof(OrganizationService.Id));

            model.Organization = organizationId;
            model.Isdeleted = 0;
            model.FixedSum = model.FixedSum == 1 ? 1 : 0;
            if (model.FixedSum != 1)
                model.ServiceSumm = null;

            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError("Name", "Укажите название услуги.");

            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid().ToString();
                _db.OrganizationServices.Add(model);
                await _db.SaveChangesAsync();
                TempData["Message"] = "Услуга создана.";
                if (IsFormModal(Request))
                    return Json(new { success = true, message = "Услуга создана." });
                return RedirectToAction(nameof(Index));
            }
            if (IsFormModal(Request))
                return Json(new { success = false, message = FirstModelError(ModelState) });
            return View(model);
        }

        [RequirePermission("children.view")]
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

            var featureGuard = EnsureOrgServicesFeature();
            if (featureGuard != null)
                return featureGuard;

            var service = await _db.OrganizationServices
                .FirstOrDefaultAsync(s => s.Id == id && s.Organization == organizationId);
            if (service == null)
                return NotFound();

            return View(service);
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, OrganizationService model)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = "Не авторизован." });
                return RedirectToAction("Login", "Account");
            }

            var featureGuard = EnsureOrgServicesFeature();
            if (featureGuard != null)
                return featureGuard;

            if (id != model.Id)
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = "Услуга не найдена." });
                return NotFound();
            }

            var service = await _db.OrganizationServices
                .FirstOrDefaultAsync(s => s.Id == id && s.Organization == organizationId);
            if (service == null)
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = "Услуга не найдена." });
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError("Name", "Укажите название услуги.");

            if (!ModelState.IsValid)
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = FirstModelError(ModelState) });
                return View(model);
            }

            service.Name = model.Name;
            service.FixedSum = model.FixedSum == 1 ? 1 : 0;
            service.ServiceSumm = service.FixedSum == 1 ? model.ServiceSumm : null;
            service.MinSumm = model.MinSumm;
            service.MaxSumm = model.MaxSumm;
            await _db.SaveChangesAsync();
            TempData["Message"] = "Изменения сохранены.";
            if (IsFormModal(Request))
                return Json(new { success = true, message = "Изменения сохранены." });
            return RedirectToAction(nameof(Index));
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = "Не авторизован." });
                return RedirectToAction("Login", "Account");
            }

            var featureGuard = EnsureOrgServicesFeature();
            if (featureGuard != null)
                return featureGuard;

            var service = await _db.OrganizationServices
                .FirstOrDefaultAsync(s => s.Id == id && s.Organization == organizationId);
            if (service == null)
            {
                if (IsFormModal(Request))
                    return Json(new { success = false, message = "Услуга не найдена." });
                return NotFound();
            }

            var usedInInvoices = await _db.InvoiceServices.AnyAsync(i => i.Service == id);
            if (usedInInvoices)
            {
                var msg = "Услугу нельзя удалить: она используется в счетах.";
                if (IsFormModal(Request))
                    return Json(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Index));
            }

            service.Isdeleted = 1;
            await _db.SaveChangesAsync();
            TempData["Message"] = "Услуга удалена.";
            if (IsFormModal(Request))
                return Json(new { success = true, message = "Услуга удалена." });
            return RedirectToAction(nameof(Index));
        }
    }
}
