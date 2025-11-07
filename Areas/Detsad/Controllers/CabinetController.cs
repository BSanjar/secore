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

        public async Task<IActionResult> Children(string search = "", string statusFilter = "active", bool debtorsOnly = false)
        {
            // ID организации (пока зашит id = 1)
            string organizationId = "1";
            
            // Получаем клиентов напрямую по организации
            var childrenData = await _db.OrganizationClients
                .Include(c => c.OrganizationClientsAdditionalFields)
                    .ThenInclude(af => af.FieldNavigation)
                .Where(c => c.Organization == organizationId)
                .ToListAsync();

            // Применяем фильтры
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                childrenData = childrenData.Where(c =>
                    (c.ClientName != null && c.ClientName.ToLower().Contains(searchLower)) ||
                    (c.ClientPhone != null && c.ClientPhone.Contains(search)) ||
                    (c.ClientEmail != null && c.ClientEmail.ToLower().Contains(searchLower)) ||
                    (c.ClientAdres != null && c.ClientAdres.ToLower().Contains(searchLower)) ||
                    (c.OrganizationClientsAdditionalFields.Any(af => 
                        af.Value != null && af.Value.ToLower().Contains(searchLower)))
                ).ToList();
            }

            // Фильтр по статусу
            if (statusFilter == "active")
            {
                childrenData = childrenData.Where(c => c.ClientStatus == 1).ToList();
            }
            else if (statusFilter == "inactive")
            {
                childrenData = childrenData.Where(c => c.ClientStatus == 0).ToList();
            }
            else if (statusFilter == "deleted")
            {
                // Предполагаем, что удаленные имеют ClientStatus = null или специальное значение
                childrenData = childrenData.Where(c => c.ClientStatus == null || c.ClientStatus < 0).ToList();
            }

            // Фильтр должников (активные с отрицательным балансом)
            if (debtorsOnly)
            {
                childrenData = childrenData.Where(c => 
                    c.ClientStatus == 1 && 
                    c.ClientBalance.HasValue && 
                    c.ClientBalance.Value < 0).ToList();
            }

            // Получаем дополнительные поля для организации
            var organizationFields = await _db.OrganizationFields
                .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
                .ToListAsync();

            // Создаем список данных для View
            var childrenViewData = childrenData
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DebtorsOnly = debtorsOnly;
            ViewBag.OrganizationFields = organizationFields;
            ViewBag.ChildrenData = childrenViewData;

            return View();
        }

        public async Task<IActionResult> GetChildInfo(string clientId)
        {
            // ID организации (пока зашит id = 1)
            string organizationId = "1";

            var client = await _db.OrganizationClients
                .Include(c => c.OrganizationClientsAdditionalFields)
                    .ThenInclude(af => af.FieldNavigation)
                .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

            if (client == null)
            {
                return NotFound();
            }

            // Получаем дополнительные поля
            var additionalFields = client.OrganizationClientsAdditionalFields
                .Where(af => af.FieldNavigation != null)
                .Select(af => new
                {
                    FieldName = af.FieldNavigation!.FieldName,
                    FieldType = af.FieldNavigation.FieldType,
                    Value = af.Value
                })
                .ToList();

            return Json(new
            {
                client = new
                {
                    id = client.Id,
                    name = client.ClientName,
                    phone = client.ClientPhone,
                    email = client.ClientEmail,
                    address = client.ClientAdres,
                    inn = client.ClientInn,
                    balance = client.ClientBalance,
                    status = client.ClientStatus,
                    createdDate = client.CreatedDate,
                    updatedDate = client.UpdatedDate,
                    logo = client.ClientLogo
                },
                additionalFields = additionalFields
            });
        }

        public async Task<IActionResult> GetInvoicesInfo(string clientId, string? invoiceId = null)
        {
            // ID организации (пока зашит id = 1)
            string organizationId = "1";

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

            if (client == null)
            {
                return NotFound();
            }

            // Получаем все инвойсы клиента
            var invoices = await _db.Invoices
                .Include(i => i.UserCreaterNavigation)
                .Where(i => i.Client == clientId)
                .OrderByDescending(i => i.DateCreated)
                .ToListAsync();

            if (!invoices.Any())
            {
                return Json(new
                {
                    client = new { name = client.ClientName },
                    invoices = new List<object>(),
                    selectedInvoice = (object?)null,
                    invoicePayments = new List<object>(),
                    transactions = new List<object>()
                });
            }

            // Выбираем инвойс (по умолчанию первый)
            var selectedInvoice = invoiceId != null 
                ? invoices.FirstOrDefault(i => i.Id == invoiceId) ?? invoices.First()
                : invoices.First();

            // Получаем журнал записей (invoice_payments) для выбранного инвойса
            var invoicePayments = await _db.InvoicePayments
                .Where(ip => ip.Invoice == selectedInvoice.Id)
                .OrderByDescending(ip => ip.DateFrom)
                .ToListAsync();

            // Получаем все транзакции по выбранному инвойсу
            var transactions = await _db.Transactions
                .Where(t => t.Invoice == selectedInvoice.Id)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return Json(new
            {
                client = new
                {
                    name = client.ClientName
                },
                invoices = invoices.Select(i => new
                {
                    id = i.Id,
                    name = i.NameInvoice ?? "Счет без названия",
                    payCode = i.PayCode
                }).ToList(),
                selectedInvoice = new
                {
                    id = selectedInvoice.Id,
                    payCode = selectedInvoice.PayCode,
                    dateCreated = selectedInvoice.DateCreated,
                    userCreater = selectedInvoice.UserCreaterNavigation?.Name ?? selectedInvoice.UserCreater ?? "Неизвестно",
                    periodicity = GetPeriodicityText(selectedInvoice.Periodicity),
                    balance = selectedInvoice.Balance,
                    autoProlongation = selectedInvoice.AutoProlongation ?? false
                },
                invoicePayments = invoicePayments.Select(ip => new
                {
                    id = ip.Id,
                    dateFrom = ip.DateFrom,
                    dateTo = ip.DateTo,
                    paymentSumm = ip.PaymentSumm,
                    paymentStatus = ip.PaymentStatus,
                    periodValue = ip.PeriodValue
                }).ToList(),
                transactions = transactions.Select(t => new
                {
                    id = t.Id,
                    transactionDate = t.TransactionDate,
                    summ = t.Summ,
                    transactionSumm = t.TransactionSumm,
                    transactionType = t.TransactionType,
                    transactionStatus = t.TransactionStatus
                }).ToList()
            });
        }

        private string GetPeriodicityText(string? periodicity)
        {
            return periodicity switch
            {
                "daily" => "Ежедневно",
                "weekly" => "Еженедельно",
                "monthly" => "Ежемесячно",
                "yearly" => "Ежегодно",
                "oneTime" => "Одноразовый",
                "any" => "Прием в любой момент",
                _ => periodicity != null && int.TryParse(periodicity, out int days) 
                    ? $"Каждые {days} дней" 
                    : periodicity ?? "Не указано"
            };
        }

        public async Task<IActionResult> GetChildDetails(string clientId)
        {
            // ID организации (пока зашит id = 1)
            string organizationId = "1";

            var client = await _db.OrganizationClients
                .Include(c => c.OrganizationClientsAdditionalFields)
                    .ThenInclude(af => af.FieldNavigation)
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

            if (client == null)
            {
                return NotFound();
            }

            // Получаем инвойсы клиента
            var invoices = await _db.Invoices
                .Include(i => i.InvoiceServices)
                    .ThenInclude(isv => isv.ServiceNavigation)
                .Where(i => i.Client == clientId)
                .ToListAsync();

            // Получаем дополнительные поля
            var additionalFields = client.OrganizationClientsAdditionalFields
                .Where(af => af.FieldNavigation != null)
                .Select(af => new
                {
                    FieldName = af.FieldNavigation!.FieldName,
                    FieldType = af.FieldNavigation.FieldType,
                    Value = af.Value
                })
                .ToList();

            return Json(new
            {
                client = new
                {
                    id = client.Id,
                    name = client.ClientName,
                    phone = client.ClientPhone,
                    email = client.ClientEmail,
                    address = client.ClientAdres,
                    balance = client.ClientBalance,
                    status = client.ClientStatus,
                    createdDate = client.CreatedDate,
                    logo = client.ClientLogo
                },
                invoices = invoices.Select(i => new
                {
                    id = i.Id,
                    dateCreated = i.DateCreated,
                    status = i.InvoiceStatus,
                    balance = i.Balance,
                    periodicity = i.Periodicity,
                    dateStart = i.DateStartInvoice,
                    payCode = i.PayCode,
                    fixedSumm = i.FixedSumm,
                    autoProlongation = i.AutoProlongation,
                    services = i.InvoiceServices.Select(isv => new
                    {
                        name = isv.ServiceNavigation?.Name,
                        summ = isv.ServiceSumm
                    }).ToList()
                }).ToList(),
                additionalFields = additionalFields
            });
        }

        public async Task<IActionResult> GetClientTransactions(string clientId)
        {
            // ID организации (пока зашит id = 1)
            string organizationId = "1";

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

            if (client == null)
            {
                return NotFound();
            }

            // Получаем все инвойсы клиента
            var invoices = await _db.Invoices
                .Where(i => i.Client == clientId)
                .Select(i => i.Id)
                .ToListAsync();

            // Получаем все транзакции по всем инвойсам клиента
            var transactions = await _db.Transactions
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice))
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return Json(new
            {
                client = new
                {
                    name = client.ClientName
                },
                transactions = transactions.Select(t => new
                {
                    id = t.Id,
                    transactionDate = t.TransactionDate,
                    summ = t.Summ,
                    transactionSumm = t.TransactionSumm,
                    transactionType = t.TransactionType,
                    transactionStatus = t.TransactionStatus,
                    invoiceId = t.Invoice
                }).ToList()
            });
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
