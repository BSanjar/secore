using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    [RequirePermission("settings.agents")]
    public class AgentsController : Controller
    {
        private readonly AppDbContext _db;

        public AgentsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var agents = await _db.Agents
                .OrderBy(a => a.Name)
                .ToListAsync();
            return View(agents);
        }

        public async Task<IActionResult> Details(string id, string dateFrom = "", string dateTo = "", string sortOrder = "desc", int page = 1, int pageSize = 15)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var agent = await _db.Agents.FindAsync(id);
            if (agent == null)
                return NotFound();

            var query = _db.Transactions.Where(t => t.Agent == id);

            if (DateTime.TryParse(dateFrom, out var fromDate))
                query = query.Where(t => t.TransactionDate != null && t.TransactionDate.Value.Date >= fromDate.Date);
            if (DateTime.TryParse(dateTo, out var toDate))
                query = query.Where(t => t.TransactionDate != null && t.TransactionDate.Value.Date <= toDate.Date.AddDays(1));

            query = sortOrder == "asc"
                ? query.OrderBy(t => t.TransactionDate)
                : query.OrderByDescending(t => t.TransactionDate);

            var totalCount = await query.CountAsync();
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Transactions = transactions;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SortOrder = sortOrder;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return View(agent);
        }

        public IActionResult Create()
        {
            return View(new Agent());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ApiLogin,ApiPassword,Name")] Agent agent)
        {
            if (string.IsNullOrWhiteSpace(agent.Name))
                ModelState.AddModelError("Name", "Укажите название агента.");

            if (ModelState.IsValid)
            {
                agent.Id = Guid.NewGuid().ToString();
                _db.Agents.Add(agent);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Агент успешно создан";
                return RedirectToAction(nameof(Index));
            }
            return View(agent);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var agent = await _db.Agents.FindAsync(id);
            if (agent == null)
                return NotFound();

            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("Id,ApiLogin,ApiPassword,Name")] Agent agent)
        {
            if (id != agent?.Id)
                return NotFound();

            if (string.IsNullOrWhiteSpace(agent.Name))
                ModelState.AddModelError("Name", "Укажите название агента.");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _db.Agents.FirstOrDefaultAsync(a => a.Id == id);
                    if (existing == null)
                        return NotFound();

                    existing.Name = agent.Name;
                    existing.ApiLogin = agent.ApiLogin;
                    if (!string.IsNullOrWhiteSpace(agent.ApiPassword))
                        existing.ApiPassword = agent.ApiPassword;
                    await _db.SaveChangesAsync();
                    TempData["Success"] = "Агент успешно обновлён";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _db.Agents.AnyAsync(e => e.Id == id))
                        return NotFound();
                    throw;
                }
            }
            return View(agent);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var agent = await _db.Agents.FindAsync(id);
            if (agent == null)
                return NotFound();

            var hasTransactions = await _db.Transactions.AnyAsync(t => t.Agent == id);
            ViewBag.HasTransactions = hasTransactions;
            return View(agent);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var agent = await _db.Agents.FindAsync(id);
            if (agent == null)
                return NotFound();

            _db.Agents.Remove(agent);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Агент удалён";
            return RedirectToAction(nameof(Index));
        }
    }
}
