using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
    [RequireAuth]
    public class InvoicesController : Controller
    {
        private readonly AppDbContext _db;

        public InvoicesController(AppDbContext db)
        {
            _db = db;
        }

        private string? GetOrganizationId()
        {
            return HttpContext.Session.GetString("OrganizationId");
        }

        [RequirePermission("invoices.view")]
        public async Task<IActionResult> Index(string search = "", string statusFilter = "")
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(i =>
                    (i.NameInvoice != null && i.NameInvoice.ToLower().Contains(term)) ||
                    (i.PayCode != null && i.PayCode.ToLower().Contains(term)) ||
                    (i.ClientNavigation != null && i.ClientNavigation.ClientName != null && i.ClientNavigation.ClientName.ToLower().Contains(term)));
            }

            var list = await query.OrderByDescending(i => i.DateCreated).ToListAsync();
            ViewBag.Search = search?.Trim() ?? "";
            ViewBag.StatusFilter = statusFilter;
            return View(list);
        }

        [RequirePermission("invoices.view")]
        public async Task<IActionResult> Details(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoiceServices)
                    .ThenInclude(s => s.ServiceNavigation)
                .Include(i => i.InvoicePayments)
                .Include(i => i.UserCreaterNavigation)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (invoice == null)
                return NotFound();

            return View(invoice);
        }

        [RequirePermission("children.create")]
        [HttpGet]
        public async Task<IActionResult> Create(string? clientId = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();

            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", clientId);
            var model = new Invoice
            {
                InvoiceStatus = "actual",
                Periodicity = "monthly",
                DateStartInvoice = DateTime.Today,
                DateCreated = DateTime.UtcNow,
                AutoProlongation = false,
                Hassameaccount = false
            };
            if (!string.IsNullOrEmpty(clientId))
                model.Client = clientId;
            return View(model);
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice model)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == model.Client && c.Organization == organizationId);
            if (client == null)
            {
                ModelState.AddModelError("Client", "Клиент не найден.");
            }

            if (string.IsNullOrWhiteSpace(model.NameInvoice))
                model.NameInvoice = "Счет " + (client?.ClientName ?? model.Client);

            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid().ToString();
                model.DateCreated = DateTime.UtcNow;
                model.UserCreater = HttpContext.Session.GetString("UserId");
                model.Balance = 0;
                _db.Invoices.Add(model);
                await _db.SaveChangesAsync();
                TempData["Message"] = "Счет создан.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();
            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", model.Client);
            return View(model);
        }

        [RequirePermission("children.create")]
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (invoice == null)
                return NotFound();

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();
            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", invoice.Client);
            return View(invoice);
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Invoice model)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            if (id != model.Id)
                return NotFound();

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (invoice == null)
                return NotFound();

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == model.Client && c.Organization == organizationId);
            if (client == null)
                ModelState.AddModelError("Client", "Клиент не найден.");

            if (ModelState.IsValid)
            {
                invoice.NameInvoice = model.NameInvoice;
                invoice.Client = model.Client;
                invoice.InvoiceStatus = model.InvoiceStatus;
                invoice.Periodicity = model.Periodicity;
                invoice.DateStartInvoice = model.DateStartInvoice;
                invoice.FixedSumm = model.FixedSumm;
                invoice.AutoProlongation = model.AutoProlongation;
                invoice.NextStartInvoice = model.NextStartInvoice;
                invoice.PayCode = model.PayCode;
                await _db.SaveChangesAsync();
                TempData["Message"] = "Изменения сохранены.";
                return RedirectToAction(nameof(Details), new { id = invoice.Id });
            }

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();
            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", model.Client);
            return View(model);
        }
    }
}
