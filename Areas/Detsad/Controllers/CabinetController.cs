using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
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
            // Для примера показываем все данные, но нужно фильтровать по организации
            
            var transactions = await _db.Transactions
                .Where(t => t.TransactionStatus == "success")
                .ToListAsync();

            var clients = await _db.OrganizationClients
                .Where(c => c.ClientStatus == 1)
                .ToListAsync();

            var invoices = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .ToListAsync();

            var now = DateTime.Now;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            // Приходы за месяц (debit транзакции)
            var monthIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                           t.TransactionDate.Value.Month == currentMonth &&
                           t.TransactionDate.Value.Year == currentYear &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100; // Конвертируем из копеек в сомы

            // Приходы за год (debit транзакции)
            var yearIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                           t.TransactionDate.Value.Year == currentYear &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100; // Конвертируем из копеек в сомы

            // Должники (клиенты с отрицательным балансом или счета с отрицательным балансом)
            var debtorsCount = clients
                .Where(c => c.ClientBalance.HasValue && c.ClientBalance.Value < 0)
                .Count();

            var debtorsAmount = clients
                .Where(c => c.ClientBalance.HasValue && c.ClientBalance.Value < 0)
                .Sum(c => Math.Abs((decimal)(c.ClientBalance ?? 0))) / 100; // Конвертируем из копеек в сомы

            // Количество клиентов
            var totalClients = clients.Count;

            // Дополнительная статистика
            var todayIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                           t.TransactionDate.Value.Date == DateTime.Today &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100;

            var activeInvoices = invoices
                .Where(i => i.InvoiceStatus == "actual")
                .Count();

            ViewBag.MonthIncome = monthIncome;
            ViewBag.YearIncome = yearIncome;
            ViewBag.DebtorsCount = debtorsCount;
            ViewBag.DebtorsAmount = debtorsAmount;
            ViewBag.TotalClients = totalClients;
            ViewBag.TodayIncome = todayIncome;
            ViewBag.ActiveInvoices = activeInvoices;

            return View();
        }

        public IActionResult CreateChild()
        {
            // Создание записи нового ребенка
            return View();
        }

        public async Task<IActionResult> Children()
        {
            // Список детей
            // TODO: Получить ID организации текущего пользователя из сессии
            // string? organizationId = HttpContext.Session.GetString("OrganizationId");
            
            var children = await _db.OrganizationClients
                .Where(c => c.OrganizationNavigation != null)
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();

            return View(children);
        }

        public async Task<IActionResult> Payments()
        {
            // Платежи для детского сада
            // TODO: Получить ID организации текущего пользователя из сессии
            // string? organizationId = HttpContext.Session.GetString("OrganizationId");
            
            var transactions = await _db.Transactions
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return View(transactions);
        }
    }
}
