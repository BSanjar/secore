using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class OrganizationFieldsController : Controller
    {
        private readonly AppDbContext _db;

        public OrganizationFieldsController(AppDbContext db)
        {
            _db = db;
        }

        private string? GetCurrentOrganizationId()
        {
            return AuthorizationHelper.GetOrganizationId(HttpContext);
        }

        // GET: OrganizationFields
        public async Task<IActionResult> Index()
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var fields = await _db.OrganizationFields
                .Where(f => f.Organization == organizationId && 
                           (f.Isdeleted == null || f.Isdeleted == 0))
                .OrderBy(f => f.FieldName)
                .ToListAsync();

            return View(fields);
        }

        // GET: OrganizationFields/Create
        public IActionResult Create()
        {
            return View(new OrganizationField { Filterbyfield = false });
        }

        // POST: OrganizationFields/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FieldName,FieldType,FieldSelectValues,Filterbyfield")] OrganizationField field)
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Генерируем ID для поля
            var allFieldIds = await _db.OrganizationFields
                .Select(f => f.Id)
                .ToListAsync();

            int newFieldId = 1;
            foreach (var idStr in allFieldIds)
            {
                if (int.TryParse(idStr, out int id) && id >= newFieldId)
                {
                    newFieldId = id + 1;
                }
            }

            field.Id = newFieldId.ToString();
            field.Organization = organizationId;
            field.Isdeleted = 0;

            // Удаляем Id, Organization, Isdeleted из ModelState
            ModelState.Remove("Id");
            ModelState.Remove("Organization");
            ModelState.Remove("Isdeleted");

            if (ModelState.IsValid)
            {
                _db.Add(field);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Дополнительное поле успешно создано";
                return RedirectToAction(nameof(Index));
            }

            return View(field);
        }

        // GET: OrganizationFields/Edit/5
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

            var field = await _db.OrganizationFields
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (field == null)
            {
                return NotFound();
            }

            return View(field);
        }

        // POST: OrganizationFields/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,FieldName,FieldType,FieldSelectValues,Filterbyfield")] OrganizationField field)
        {
            if (id != field.Id)
            {
                return NotFound();
            }

            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var existingField = await _db.OrganizationFields
                .FirstOrDefaultAsync(f => f.Id == id && 
                                         f.Organization == organizationId &&
                                         (f.Isdeleted == null || f.Isdeleted == 0));

            if (existingField == null)
            {
                return NotFound();
            }

            // Обновляем данные
            existingField.FieldName = field.FieldName;
            existingField.FieldType = field.FieldType;
            existingField.FieldSelectValues = field.FieldSelectValues;
            existingField.Filterbyfield = field.Filterbyfield;

            // Удаляем из ModelState
            ModelState.Remove("Organization");
            ModelState.Remove("Isdeleted");

            if (ModelState.IsValid)
            {
                try
                {
                    await _db.SaveChangesAsync();
                    TempData["Success"] = "Дополнительное поле успешно обновлено";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FieldExists(field.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(existingField);
        }

        // GET: OrganizationFields/Delete/5
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

            var field = await _db.OrganizationFields
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (field == null)
            {
                return NotFound();
            }

            return View(field);
        }

        // POST: OrganizationFields/DeleteConfirmed/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var organizationId = GetCurrentOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var field = await _db.OrganizationFields
                .FirstOrDefaultAsync(m => m.Id == id && 
                                         m.Organization == organizationId &&
                                         (m.Isdeleted == null || m.Isdeleted == 0));

            if (field != null)
            {
                field.Isdeleted = 1;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Дополнительное поле успешно удалено";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool FieldExists(string id)
        {
            return (_db.OrganizationFields?.Any(e => e.Id == id && (e.Isdeleted == null || e.Isdeleted == 0))).GetValueOrDefault();
        }
    }
}

