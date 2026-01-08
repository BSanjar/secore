using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Areas.Simple.Controllers
{
    [Area("Simple")]
    [RequireAuth]
    public class CabinetController : Controller
    {

        private readonly AppDbContext _db;
        public CabinetController(AppDbContext db) => _db = db;

        [RequirePermission("dashboard.view")]
        public async Task<IActionResult> Index()
        {
            // Короткая сводка по последним операциям (только debit, success)
            var tx = await _db.Transactions
                .Include(t => t.InvoiceNavigation)
                .Where(t => t.TransactionType == "debit" && t.TransactionStatus == "success")
                .OrderByDescending(t => t.TransactionDate)
                .Take(10)
                .ToListAsync();

            ViewBag.TodayIncome = tx
                .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Date == DateTime.Today)
                .Sum(t => (t.Summ ?? 0) / 100m);

            ViewBag.ThisMonthIncome = tx
                .Where(t => t.TransactionDate.HasValue &&
                            t.TransactionDate.Value.Month == DateTime.Now.Month &&
                            t.TransactionDate.Value.Year == DateTime.Now.Year)
                .Sum(t => (t.Summ ?? 0) / 100m);

            ViewBag.TotalTransactions = await _db.Transactions.CountAsync();

            return View(tx); // простая лента последних поступлений
        }

    }
}
