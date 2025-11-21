using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Areas.Simple.ViewModels;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using WebApplication1.Modules.GenericModule.Models;
using WebApplication1.Modules.GenericModule.Services;

namespace WebApplication1.Areas.Simple.Controllers
{
    [Area("Simple")]
    public class PaymentsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ITableSource<PaymentListItemVm> _paymentsTableSource;

        public PaymentsController(
            AppDbContext db,
            ITableSource<PaymentListItemVm> paymentsTableSource)
        {
            _db = db;
            _paymentsTableSource = paymentsTableSource;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] SimplePaymentsFilterParams filters)
        {
            filters ??= new SimplePaymentsFilterParams();

            if (filters.PageNumber <= 0)
            {
                filters.PageNumber = 1;
            }

            if (filters.PageSize <= 0)
            {
                filters.PageSize = 10;
            }

            var result = await _paymentsTableSource.GetDataAsync(filters);

            var tableViewModel = new GenericTableViewModel<object>
            {
                Items = result.Items.Cast<object>().ToList(),
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                Filters = filters,
                TableTitle = "История платежей",
                PartialHeaderViewName = "~/Areas/Simple/Views/Payments/Partials/_PaymentsTableHeader.cshtml",
                PartialRowViewName = "~/Areas/Simple/Views/Payments/Partials/_PaymentsTableRow.cshtml",
                LoadUrl = Url.Action(nameof(Index), "Payments", new { area = "Simple" }) ?? string.Empty,
                ExportUrl = null,
                SearchableColumns = new Dictionary<string, string>
                {
                    ["ClientName"] = "Клиент",
                    ["InvoiceName"] = "Счёт",
                    ["Status"] = "Статус"
                },
                FilterPartialViewName = "~/Areas/Simple/Views/Payments/Partials/_PaymentsFilters.cshtml",
                EmptyStateTitle = "Нет платежей",
                EmptyStateDescription = "Создайте первый платёж, чтобы увидеть историю."
            };

            var pageViewModel = new SimplePaymentsIndexViewModel
            {
                Table = tableViewModel,
                CreatePaymentUrl = Url.Action(nameof(Create), "Payments", new { area = "Simple" }) ?? string.Empty,
                FlashMessage = TempData["Success"] as string
            };

            return View(pageViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // список клиентов для выбора
            ViewBag.Clients = await _db.OrganizationClients
                .Where(c => c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();

            return View(new CreatePaymentViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePaymentViewModel model)
        {
            // валидация
            if (string.IsNullOrWhiteSpace(model.ClientId))
                ModelState.AddModelError(nameof(model.ClientId), "Выберите клиента");
            if (model.AmountSom <= 0)
                ModelState.AddModelError(nameof(model.AmountSom), "Сумма должна быть больше 0");

            if (!ModelState.IsValid)
            {
                ViewBag.Clients = await _db.OrganizationClients
                    .Where(c => c.ClientStatus == 1)
                    .OrderBy(c => c.ClientName)
                    .Select(c => new { c.Id, c.ClientName })
                    .ToListAsync();
                return View(model);
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1) создаем "разовый" инвойс без баланса и без периодичности
                var invoiceId = Guid.NewGuid().ToString();
                var invoice = new Invoice
                {
                    Id = invoiceId,
                    DateCreated = DateHelper.NowForTimestamp(),
                    UserCreater = User?.Identity?.Name, // или id пользователя из claims
                    InvoiceStatus = "actual",
                    Periodicity = "oneTime",
                    DateStartInvoice = DateHelper.NowForTimestamp(),
                    Balance = null,               // !!! не трогаем баланс
                    PayCode = string.IsNullOrWhiteSpace(model.PayCode)
                        ? new Random().Next(100000, 999999).ToString()
                        : model.PayCode,
                    Client = model.ClientId,
                    FixedSumm = model.AmountSom.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    AutoProlongation = false,
                    NextStartInvoice = null,
                    NameInvoice = string.IsNullOrWhiteSpace(model.InvoiceName)
                        ? "Разовый платёж"
                        : model.InvoiceName
                };
                _db.Invoices.Add(invoice);
                await _db.SaveChangesAsync();

                // 2) создаем транзакцию DEBIT, сумма в тыйынах
                var tyiyn = decimal.Round(model.AmountSom * 100m, 0, MidpointRounding.AwayFromZero);
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionDate = DateHelper.NowForTimestamp(),
                    TransactionStatus = "success",      // или in_process, если нужна внешняя проверка
                    Summ = tyiyn,
                    TransactionSumm = tyiyn,            // если без комиссии — равны
                    Invoice = invoiceId,
                    TransactionType = "debit"
                };
                _db.Transactions.Add(transaction);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                TempData["Success"] = $"Платёж создан. PayCode: {invoice.PayCode}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Ошибка при создании платежа: " + ex.Message);
                ViewBag.Clients = await _db.OrganizationClients
                    .Where(c => c.ClientStatus == 1)
                    .OrderBy(c => c.ClientName)
                    .Select(c => new { c.Id, c.ClientName })
                    .ToListAsync();
                return View(model);
            }
        }
    }
}
 