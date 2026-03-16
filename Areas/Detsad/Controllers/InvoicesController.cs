using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using WebApplication1.Services;
using ClosedXML.Excel;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
    [RequireAuth]
    public class InvoicesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ViewRenderService _viewRender;

        public InvoicesController(AppDbContext db, ViewRenderService viewRender)
        {
            _db = db;
            _viewRender = viewRender;
        }

        private string? GetOrganizationId()
        {
            return HttpContext.Session.GetString("OrganizationId");
        }

        [RequirePermission("invoices.view")]
        public async Task<IActionResult> Index(
            string search = "",
            string statusFilter = "",
            string clientId = "",
            string periodicity = "",
            string dateCreatedFrom = "",
            string dateCreatedTo = "")
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);

            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(i => i.Client == clientId);

            if (!string.IsNullOrWhiteSpace(periodicity))
                query = query.Where(i => i.Periodicity == periodicity);

            if (DateTime.TryParse(dateCreatedFrom, out var fromDate))
                query = query.Where(i => i.DateCreated >= fromDate.Date);

            if (DateTime.TryParse(dateCreatedTo, out var toDate))
                query = query.Where(i => i.DateCreated != null && i.DateCreated.Value.Date <= toDate.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(i =>
                    (i.NameInvoice != null && i.NameInvoice.ToLower().Contains(term)) ||
                    (i.PayCode != null && i.PayCode.ToLower().Contains(term)) ||
                    (i.ClientNavigation != null && i.ClientNavigation.ClientName != null && i.ClientNavigation.ClientName.ToLower().Contains(term)));
            }

            var list = await query.OrderByDescending(i => i.DateCreated).ToListAsync();

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();

            var selectedClientName = !string.IsNullOrEmpty(clientId)
                ? clients.FirstOrDefault(c => c.Id == clientId)?.ClientName
                : null;

            ViewBag.Search = search?.Trim() ?? "";
            ViewBag.StatusFilter = statusFilter;
            ViewBag.ClientId = clientId;
            ViewBag.SelectedClientName = selectedClientName;
            ViewBag.Periodicity = periodicity;
            ViewBag.DateCreatedFrom = dateCreatedFrom;
            ViewBag.DateCreatedTo = dateCreatedTo;
            ViewBag.Clients = clients;
            ViewBag.ReturnUrl = Url.Action("Index", new { search = search?.Trim(), statusFilter, clientId, periodicity, dateCreatedFrom, dateCreatedTo });
            return View(list);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            string search = "",
            string statusFilter = "",
            string clientId = "",
            string periodicity = "",
            string dateCreatedFrom = "",
            string dateCreatedTo = "")
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);
            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(i => i.Client == clientId);
            if (!string.IsNullOrWhiteSpace(periodicity))
                query = query.Where(i => i.Periodicity == periodicity);
            if (DateTime.TryParse(dateCreatedFrom, out var fromDate))
                query = query.Where(i => i.DateCreated >= fromDate.Date);
            if (DateTime.TryParse(dateCreatedTo, out var toDate))
                query = query.Where(i => i.DateCreated != null && i.DateCreated.Value.Date <= toDate.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(i =>
                    (i.NameInvoice != null && i.NameInvoice.ToLower().Contains(term)) ||
                    (i.PayCode != null && i.PayCode.ToLower().Contains(term)) ||
                    (i.ClientNavigation != null && i.ClientNavigation.ClientName != null && i.ClientNavigation.ClientName.ToLower().Contains(term)));
            }

            var list = await query.OrderByDescending(i => i.DateCreated).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Отчетность");

            ws.Cell(1, 1).Value = "Название / Лицевой счёт";
            ws.Cell(1, 2).Value = "Клиент";
            ws.Cell(1, 3).Value = "Статус";
            ws.Cell(1, 4).Value = "Периодичность";
            ws.Cell(1, 5).Value = "Баланс (сом)";
            ws.Cell(1, 6).Value = "Дата создания";
            var headerRow = ws.Range(1, 1, 1, 6);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            var statusText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["actual"] = "Активный", ["suspended"] = "Приостановлен", ["closed"] = "Закрыт"
            };
            var periodicityText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["daily"] = "Ежедневно", ["weekly"] = "Еженедельно", ["monthly"] = "Ежемесячно",
                ["yearly"] = "Ежегодно", ["oneTime"] = "Разовая", ["any"] = "Любая"
            };

            int row = 2;
            foreach (var inv in list)
            {
                ws.Cell(row, 1).Value = inv.NameInvoice ?? inv.PayCode ?? inv.Id ?? "";
                ws.Cell(row, 2).Value = inv.ClientNavigation?.ClientName ?? inv.Client ?? "";
                ws.Cell(row, 3).Value = statusText.TryGetValue(inv.InvoiceStatus ?? "", out var s) ? s : inv.InvoiceStatus ?? "";
                ws.Cell(row, 4).Value = periodicityText.TryGetValue(inv.Periodicity ?? "", out var p) ? p : inv.Periodicity ?? "";
                ws.Cell(row, 5).Value = inv.Balance.HasValue ? inv.Balance.Value / 100m : 0m;
                ws.Cell(row, 6).Value = inv.DateCreated.HasValue ? inv.DateCreated.Value.ToString("dd.MM.yyyy") : "";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream, false);
            stream.Position = 0;
            var fileName = $"Otchetnost_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> ExportPaymentsExcel(
            string search = "",
            string statusFilter = "",
            string clientId = "",
            string periodicity = "",
            string dateCreatedFrom = "",
            string dateCreatedTo = "")
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoicePayments)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);
            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(i => i.Client == clientId);
            if (!string.IsNullOrWhiteSpace(periodicity))
                query = query.Where(i => i.Periodicity == periodicity);
            if (DateTime.TryParse(dateCreatedFrom, out var fromDate))
                query = query.Where(i => i.DateCreated >= fromDate.Date);
            if (DateTime.TryParse(dateCreatedTo, out var toDate))
                query = query.Where(i => i.DateCreated != null && i.DateCreated.Value.Date <= toDate.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(i =>
                    (i.NameInvoice != null && i.NameInvoice.ToLower().Contains(term)) ||
                    (i.PayCode != null && i.PayCode.ToLower().Contains(term)) ||
                    (i.ClientNavigation != null && i.ClientNavigation.ClientName != null && i.ClientNavigation.ClientName.ToLower().Contains(term)));
            }

            var list = await query.OrderBy(i => i.ClientNavigation!.ClientName).ThenBy(i => i.PayCode).ThenByDescending(i => i.DateCreated).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Платежи по счетам");

            ws.Cell(1, 1).Value = "Номер счета";
            ws.Cell(1, 2).Value = "Лицевой счет (PayCode)";
            ws.Cell(1, 3).Value = "Клиент";
            ws.Cell(1, 4).Value = "Период с";
            ws.Cell(1, 5).Value = "Период по";
            ws.Cell(1, 6).Value = "Значение периода";
            ws.Cell(1, 7).Value = "Сумма (сом)";
            ws.Cell(1, 8).Value = "Статус платежа";
            var headerRow = ws.Range(1, 1, 1, 8);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            var paymentStatusText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["paid"] = "Оплачено", ["non_paid"] = "Не оплачено", ["anulated"] = "Аннулировано"
            };

            int row = 2;
            foreach (var inv in list)
            {
                var payments = (inv.InvoicePayments ?? new List<InvoicePayment>()).OrderBy(p => p.DateFrom ?? DateTime.MinValue).ToList();
                if (payments.Count == 0)
                {
                    ws.Cell(row, 1).Value = inv.Id ?? "";
                    ws.Cell(row, 2).Value = inv.PayCode ?? "";
                    ws.Cell(row, 3).Value = inv.ClientNavigation?.ClientName ?? inv.Client ?? "";
                    ws.Cell(row, 4).Value = "";
                    ws.Cell(row, 5).Value = "";
                    ws.Cell(row, 6).Value = "";
                    ws.Cell(row, 7).Value = "";
                    ws.Cell(row, 8).Value = "Нет записей";
                    row++;
                    continue;
                }
                foreach (var ip in payments)
                {
                    ws.Cell(row, 1).Value = inv.Id ?? "";
                    ws.Cell(row, 2).Value = inv.PayCode ?? "";
                    ws.Cell(row, 3).Value = inv.ClientNavigation?.ClientName ?? inv.Client ?? "";
                    ws.Cell(row, 4).Value = ip.DateFrom.HasValue ? ip.DateFrom.Value.ToString("dd.MM.yyyy") : "";
                    ws.Cell(row, 5).Value = ip.DateTo.HasValue ? ip.DateTo.Value.ToString("dd.MM.yyyy") : "";
                    ws.Cell(row, 6).Value = ip.PeriodValue ?? "";
                    ws.Cell(row, 7).Value = ip.PaymentSumm.HasValue ? ip.PaymentSumm.Value / 100m : 0m;
                    ws.Cell(row, 8).Value = paymentStatusText.TryGetValue(ip.PaymentStatus ?? "", out var ps) ? ps : ip.PaymentStatus ?? "";
                    row++;
                }
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream, false);
            stream.Position = 0;
            var fileName = $"Platezhi_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> ExportInvoicePaymentsExcel(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoicePayments)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (invoice == null)
                return NotFound();

            var payments = (invoice.InvoicePayments ?? new List<InvoicePayment>()).OrderBy(p => p.DateFrom ?? DateTime.MinValue).ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Платежи по счёту");

            ws.Cell(1, 1).Value = "Период с";
            ws.Cell(1, 2).Value = "Период по";
            ws.Cell(1, 3).Value = "Значение периода";
            ws.Cell(1, 4).Value = "Сумма (сом)";
            ws.Cell(1, 5).Value = "Статус платежа";
            var headerRow = ws.Range(1, 1, 1, 5);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            var paymentStatusText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["paid"] = "Оплачено", ["non_paid"] = "Не оплачено", ["anulated"] = "Аннулировано"
            };

            int row = 2;
            if (payments.Count == 0)
            {
                ws.Cell(row, 1).Value = "";
                ws.Cell(row, 2).Value = "";
                ws.Cell(row, 3).Value = "";
                ws.Cell(row, 4).Value = "";
                ws.Cell(row, 5).Value = "Нет записей";
                row++;
            }
            else
            {
                foreach (var ip in payments)
                {
                    ws.Cell(row, 1).Value = ip.DateFrom.HasValue ? ip.DateFrom.Value.ToString("dd.MM.yyyy") : "";
                    ws.Cell(row, 2).Value = ip.DateTo.HasValue ? ip.DateTo.Value.ToString("dd.MM.yyyy") : "";
                    ws.Cell(row, 3).Value = ip.PeriodValue ?? "";
                    ws.Cell(row, 4).Value = ip.PaymentSumm.HasValue ? ip.PaymentSumm.Value / 100m : 0m;
                    ws.Cell(row, 5).Value = paymentStatusText.TryGetValue(ip.PaymentStatus ?? "", out var ps) ? ps : ip.PaymentStatus ?? "";
                    row++;
                }
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream, false);
            stream.Position = 0;
            var safeName = (invoice.PayCode ?? invoice.Id ?? "invoice").Replace(" ", "_");
            var fileName = $"Platezhi_{safeName}_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [RequirePermission("invoices.view")]
        public async Task<IActionResult> Details(string id, string? returnUrl = null)
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

            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index");
            return View(invoice);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> DownloadPdf(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .ThenInclude(c => c!.OrganizationNavigation)
                .Include(i => i.InvoiceServices)
                .ThenInclude(s => s.ServiceNavigation)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (invoice == null)
                return NotFound();

            ViewBag.ReceiverName = invoice.ClientNavigation?.OrganizationNavigation?.Name ?? "—";
            ViewBag.Inn = "—";
            ViewBag.Bank = "—";
            ViewBag.Account = "—";

            var html = await _viewRender.RenderViewToStringAsync(ControllerContext, "InvoicePdf", invoice);

            await new BrowserFetcher().DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);
            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                DisplayHeaderFooter = false,
                MarginOptions = new MarginOptions { Top = "8mm", Right = "8mm", Bottom = "8mm", Left = "8mm" }
            });

            var fileName = $"invoice_{(invoice.PayCode ?? invoice.Id ?? "invoice").Replace(" ", "_")}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> DownloadReceiptPdf(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var payment = await _db.InvoicePayments
                .Include(ip => ip.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
                .ThenInclude(c => c!.OrganizationNavigation)
                .FirstOrDefaultAsync(ip => ip.Id == id
                    && ip.InvoiceNavigation != null
                    && ip.InvoiceNavigation.ClientNavigation != null
                    && ip.InvoiceNavigation.ClientNavigation.Organization == organizationId);

            if (payment == null || payment.PaymentStatus != "paid")
                return NotFound();

            var html = await _viewRender.RenderViewToStringAsync(ControllerContext, "PaymentReceiptPdf", payment);

            await new BrowserFetcher().DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html);
            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                DisplayHeaderFooter = false,
                MarginOptions = new MarginOptions { Top = "8mm", Right = "8mm", Bottom = "8mm", Left = "8mm" }
            });

            var fileName = $"receipt_{(payment.PeriodValue ?? payment.Id ?? "payment").Replace(" ", "_")}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> DownloadStatementPdf(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .ThenInclude(c => c!.OrganizationNavigation)
                .Include(i => i.InvoicePayments)
                .Include(i => i.Transactions)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (invoice == null)
                return NotFound();

            try
            {
                var ru = new System.Globalization.CultureInfo("ru-RU");
                var periodStart = invoice.DateStartInvoice ?? invoice.DateCreated?.Date ?? DateTime.Today.AddMonths(-3);
                var periodEnd = DateTime.Today;

                var paymentsOrdered = (invoice.InvoicePayments ?? new List<InvoicePayment>()).OrderBy(p => p.DateFrom ?? DateTime.MinValue).ToList();
                var transactionsOrdered = (invoice.Transactions ?? new List<Transaction>()).Where(t => t.TransactionStatus == "success").OrderBy(t => t.TransactionDate ?? DateTime.MinValue).ToList();

                var rows = new List<StatementRowDto>();
                decimal runningBalance = 0m;

                foreach (var ip in paymentsOrdered)
                {
                    var date = ip.DateFrom ?? ip.DateTo ?? DateTime.MinValue;
                    var amount = (ip.PaymentSumm ?? 0) / 100m;
                    runningBalance -= amount;
                    rows.Add(new StatementRowDto
                    {
                        Date = date,
                        Type = "accrual",
                        TypeBadgeClass = "accrual",
                        TypeText = "Начисление",
                        Description = "Начисление за период " + (ip.PeriodValue ?? date.ToString("MMMM yyyy", ru)),
                        DocumentId = invoice.Id,
                        AccrualAmount = amount,
                        PaymentAmount = null,
                        Commission = 0m,
                        BalanceAfter = runningBalance
                    });
                }

                foreach (var t in transactionsOrdered)
                {
                    var date = t.TransactionDate ?? DateTime.MinValue;
                    var amount = (t.Summ ?? 0) / 100m;
                    var commission = ((t.TransactionSumm ?? 0) - (t.Summ ?? 0)) / 100m;
                    runningBalance += amount;
                    rows.Add(new StatementRowDto
                    {
                        Date = date,
                        Type = "payment",
                        TypeBadgeClass = "payment",
                        TypeText = "Оплата",
                        Description = t.TransactionType == "debit" ? "Оплата по счету" : "Операция",
                        DocumentId = t.TxnId ?? t.Id,
                        AccrualAmount = null,
                        PaymentAmount = amount,
                        Commission = commission,
                        BalanceAfter = runningBalance
                    });
                }

                rows = rows.OrderBy(r => r.Date).ToList();
                runningBalance = 0m;
                foreach (var r in rows)
                {
                    if (r.Type == "accrual") runningBalance -= r.AccrualAmount ?? 0;
                    else runningBalance += r.PaymentAmount ?? 0;
                    r.BalanceAfter = runningBalance;
                }

                var totalAccrued = paymentsOrdered.Sum(p => (p.PaymentSumm ?? 0) / 100m);
                var totalPaid = transactionsOrdered.Where(t => t.TransactionType == "debit").Sum(t => (t.Summ ?? 0) / 100m);
                var currentBalance = (invoice.Balance ?? 0) / 100m;

                ViewBag.Operations = rows;
                ViewBag.PeriodStart = periodStart;
                ViewBag.PeriodEnd = periodEnd;
                ViewBag.TotalAccrued = totalAccrued;
                ViewBag.TotalPaid = totalPaid;
                ViewBag.OpeningBalance = 0m;
                ViewBag.ClosingBalance = currentBalance;
                ViewBag.StatementNumber = "STM-" + (invoice.Id?.Length > 8 ? invoice.Id.Substring(0, 8) : invoice.Id ?? "—");

                var html = await _viewRender.RenderViewToStringAsync(ControllerContext, "StatementPdf", invoice);

                await new BrowserFetcher().DownloadAsync();
                await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
                await using var page = await browser.NewPageAsync();
                await page.SetContentAsync(html);
                var pdfBytes = await page.PdfDataAsync(new PdfOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true,
                    DisplayHeaderFooter = false,
                    MarginOptions = new MarginOptions { Top = "8mm", Right = "8mm", Bottom = "8mm", Left = "8mm" }
                });

                var fileName = $"statement_{(invoice.PayCode ?? invoice.Id ?? "invoice").Replace(" ", "_")}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception)
            {
                TempData["Error"] = "Не удалось сформировать выписку в PDF. Попробуйте позже.";
                return RedirectToAction(nameof(Details), new { id });
            }
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
        public async Task<IActionResult> Edit(string id, string? returnUrl = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoiceServices)
                .ThenInclude(s => s.ServiceNavigation)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (invoice == null)
                return NotFound();

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();

            var orgServices = await _db.OrganizationServices
                .Where(s => s.Organization == organizationId && (s.Isdeleted == null || s.Isdeleted == 0))
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.ServiceSumm, s.MinSumm, s.MaxSumm })
                .ToListAsync();

            var settings = await _db.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", invoice.Client);
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index");
            ViewBag.OrganizationServices = orgServices;
            ViewBag.DisableInvoiceServiceSelection = settings?.DisableInvoiceServiceSelection ?? false;
            ViewBag.AllowedHassameaccount = settings?.AllowedHassameaccount ?? false;
            return View(invoice);
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Invoice model, string? returnUrl = null, string? serviceItemsJson = null, decimal? manualServicePriceSom = null, bool? useExistingPayCode = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            if (id != model.Id)
                return NotFound();

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoiceServices)
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
                invoice.DateEndInvoice = model.DateEndInvoice;
                invoice.AutoProlongation = model.AutoProlongation;
                invoice.NextStartInvoice = model.NextStartInvoice;
                invoice.PayCode = model.PayCode;
                invoice.Hassameaccount = useExistingPayCode == true;

                if (manualServicePriceSom.HasValue)
                {
                    var totalTyiyn = (decimal)(manualServicePriceSom.Value * 100m);
                    invoice.FixedSumm = totalTyiyn;
                    foreach (var existing in invoice.InvoiceServices.ToList())
                        _db.InvoiceServices.Remove(existing);
                    _db.InvoiceServices.Add(new InvoiceService
                    {
                        Id = Guid.NewGuid().ToString(),
                        Invoice = invoice.Id,
                        Service = null,
                        ServiceSumm = totalTyiyn
                    });
                }
                else if (!string.IsNullOrWhiteSpace(serviceItemsJson))
                {
                    List<EditServiceItemDto>? items = null;
                    try
                    {
                        items = JsonSerializer.Deserialize<List<EditServiceItemDto>>(serviceItemsJson);
                    }
                    catch { }
                    if (items != null && items.Count > 0)
                    {
                        var serviceIds = items.Select(x => x.ServiceId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                        var orgServices = await _db.OrganizationServices
                            .Where(s => s.Organization == organizationId && serviceIds.Contains(s.Id))
                            .ToDictionaryAsync(s => s.Id);
                        decimal totalTyiyn = 0;
                        foreach (var existing in invoice.InvoiceServices.ToList())
                            _db.InvoiceServices.Remove(existing);
                        foreach (var item in items)
                        {
                            if (string.IsNullOrEmpty(item.ServiceId) || !orgServices.TryGetValue(item.ServiceId, out var orgService))
                                continue;
                            var qty = Math.Max(1, item.Qty ?? 1);
                            var serviceSummTyiyn = orgService.ServiceSumm ?? 0;
                            var lineTotal = serviceSummTyiyn * qty;
                            totalTyiyn += lineTotal;
                            _db.InvoiceServices.Add(new InvoiceService
                            {
                                Id = Guid.NewGuid().ToString(),
                                Invoice = invoice.Id,
                                Service = orgService.Id,
                                ServiceSumm = lineTotal
                            });
                        }
                        invoice.FixedSumm = totalTyiyn;
                    }
                }
                else
                {
                    invoice.FixedSumm = invoice.InvoiceServices.Any() ? invoice.InvoiceServices.Sum(s => s.ServiceSumm ?? 0) : invoice.FixedSumm;
                }

                await _db.SaveChangesAsync();
                TempData["Message"] = "Изменения сохранены.";
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Details), new { id = invoice.Id });
            }

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();
            var orgServicesList = await _db.OrganizationServices
                .Where(s => s.Organization == organizationId && (s.Isdeleted == null || s.Isdeleted == 0))
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.ServiceSumm, s.MinSumm, s.MaxSumm })
                .ToListAsync();
            var settings = await _db.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);
            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", model.Client);
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index");
            ViewBag.OrganizationServices = orgServicesList;
            ViewBag.DisableInvoiceServiceSelection = settings?.DisableInvoiceServiceSelection ?? false;
            ViewBag.AllowedHassameaccount = settings?.AllowedHassameaccount ?? false;
            return View(model);
        }

        /// <summary>
        /// API для сохранения счёта из модального окна редактирования. Возвращает JSON.
        /// </summary>
        [RequirePermission("children.create")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateInvoice([FromBody] UpdateInvoiceRequest request)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });
            if (request == null || string.IsNullOrEmpty(request.Id))
                return Json(new { success = false, message = "Не указан счёт." });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoiceServices)
                .FirstOrDefaultAsync(i => i.Id == request.Id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (invoice == null)
                return Json(new { success = false, message = "Счёт не найден." });

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == request.Client && c.Organization == organizationId);
            if (client == null)
                return Json(new { success = false, message = "Клиент не найден." });

            invoice.NameInvoice = request.NameInvoice;
            invoice.Client = request.Client;
            invoice.InvoiceStatus = request.InvoiceStatus ?? "actual";
            invoice.Periodicity = request.Periodicity ?? "monthly";
            invoice.DateStartInvoice = request.DateStartInvoice;
            invoice.DateEndInvoice = request.DateEndInvoice;
            invoice.AutoProlongation = request.AutoProlongation;
            invoice.NextStartInvoice = request.NextStartInvoice;
            invoice.PayCode = request.PayCode;
            invoice.Hassameaccount = request.Hassameaccount;

            if (request.ManualServicePriceSom.HasValue)
            {
                var totalTyiyn = (decimal)(request.ManualServicePriceSom.Value * 100m);
                invoice.FixedSumm = totalTyiyn;
                foreach (var existing in invoice.InvoiceServices.ToList())
                    _db.InvoiceServices.Remove(existing);
                _db.InvoiceServices.Add(new InvoiceService
                {
                    Id = Guid.NewGuid().ToString(),
                    Invoice = invoice.Id,
                    Service = null,
                    ServiceSumm = totalTyiyn
                });
            }
            else if (request.ServiceItems != null && request.ServiceItems.Count > 0)
            {
                var serviceIds = request.ServiceItems.Select(x => x.ServiceId).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                var orgServices = await _db.OrganizationServices
                    .Where(s => s.Organization == organizationId && serviceIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id);
                decimal totalTyiyn = 0;
                foreach (var existing in invoice.InvoiceServices.ToList())
                    _db.InvoiceServices.Remove(existing);
                foreach (var item in request.ServiceItems)
                {
                    if (string.IsNullOrEmpty(item.ServiceId) || !orgServices.TryGetValue(item.ServiceId, out var orgService))
                        continue;
                    var qty = Math.Max(1, item.Qty ?? 1);
                    var serviceSummTyiyn = orgService.ServiceSumm ?? 0;
                    var lineTotal = serviceSummTyiyn * qty;
                    totalTyiyn += lineTotal;
                    _db.InvoiceServices.Add(new InvoiceService
                    {
                        Id = Guid.NewGuid().ToString(),
                        Invoice = invoice.Id,
                        Service = orgService.Id,
                        ServiceSumm = lineTotal
                    });
                }
                invoice.FixedSumm = totalTyiyn;
            }
            else
            {
                invoice.FixedSumm = invoice.InvoiceServices.Any() ? invoice.InvoiceServices.Sum(s => s.ServiceSumm ?? 0) : invoice.FixedSumm;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Изменения сохранены." });
        }

        public class UpdateInvoiceRequest
        {
            public string? Id { get; set; }
            public string? Client { get; set; }
            public string? NameInvoice { get; set; }
            public string? PayCode { get; set; }
            public string? Periodicity { get; set; }
            public string? InvoiceStatus { get; set; }
            public DateTime? DateStartInvoice { get; set; }
            public DateTime? DateEndInvoice { get; set; }
            public bool AutoProlongation { get; set; }
            public DateTime? NextStartInvoice { get; set; }
            public bool Hassameaccount { get; set; }
            public decimal? ManualServicePriceSom { get; set; }
            public List<UpdateInvoiceServiceItemDto>? ServiceItems { get; set; }
        }

        public class UpdateInvoiceServiceItemDto
        {
            public string? ServiceId { get; set; }
            public int? Qty { get; set; }
        }

        private class EditServiceItemDto
        {
            public string? ServiceId { get; set; }
            public int? Qty { get; set; }
        }
    }

    public class StatementRowDto
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";
        public string TypeBadgeClass { get; set; } = "";
        public string TypeText { get; set; } = "";
        public string Description { get; set; } = "";
        public string? DocumentId { get; set; }
        public decimal? AccrualAmount { get; set; }
        public decimal? PaymentAmount { get; set; }
        public decimal Commission { get; set; }
        public decimal BalanceAfter { get; set; }
    }
}
