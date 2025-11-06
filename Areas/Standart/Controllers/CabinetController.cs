using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Areas.Standart.Controllers
{
    [Area("Standart")]
    public class CabinetController : Controller
    {
        private readonly AppDbContext _db;
        
        public CabinetController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            // TODO: Получить ID организации текущего пользователя из сессии
            // string? organizationId = HttpContext.Session.GetString("OrganizationId");
            // Для примера показываем все транзакции, но нужно фильтровать по организации
            
            var transactions = await _db.Transactions
                .Where(t => t.TransactionStatus == "success")
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // Статистика
            var totalIncome = transactions
                .Where(t => t.Summ.HasValue && t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100; // Конвертируем из копеек в сомы

            var totalTransactions = transactions.Count;
            
            var todayIncome = transactions
                .Where(t => t.TransactionDate.HasValue && 
                           t.TransactionDate.Value.Date == DateTime.Today &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100; // Конвертируем из копеек в сомы

            var thisMonthIncome = transactions
                .Where(t => t.TransactionDate.HasValue && 
                           t.TransactionDate.Value.Month == DateTime.Now.Month &&
                           t.TransactionDate.Value.Year == DateTime.Now.Year &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100; // Конвертируем из копеек в сомы

            ViewBag.TotalIncome = totalIncome;
            ViewBag.TotalTransactions = totalTransactions;
            ViewBag.TodayIncome = todayIncome;
            ViewBag.ThisMonthIncome = thisMonthIncome;

            return View();
        }

        public async Task<IActionResult> PaymentHistory()
        {
            // TODO: Получить ID организации текущего пользователя из сессии
            // string? organizationId = HttpContext.Session.GetString("OrganizationId");
            // Для примера показываем все транзакции, но нужно фильтровать по организации
            
            var transactions = await _db.Transactions
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View(transactions);
        }

        public async Task<IActionResult> Invoices()
        {
            // TODO: Получить ID организации текущего пользователя из сессии
            // string? organizationId = HttpContext.Session.GetString("OrganizationId");
            // Для примера показываем все счета, но нужно фильтровать по организации
            
            var invoices = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoiceServices)
                    .ThenInclude(isv => isv.ServiceNavigation)
                .OrderByDescending(i => i.DateCreated)
                .ToListAsync();

            return View(invoices);
        }
    }
}

