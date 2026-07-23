using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;
using WebApplication1.Dtos;
using ClosedXML.Excel;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class InvoicesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ViewRenderService _viewRender;
        private readonly OperationsByInvoices _operationsByInvoices;
        private readonly ExcelExportService _excelExportService;
        private readonly PuppeteerPdfBrowserService _puppeteerPdf;
        private readonly InvoiceQrService _invoiceQrService;
        private readonly ILogger<InvoicesController> _logger;
        private readonly ICurrentTenantService _currentTenantService;

        public InvoicesController(
            AppDbContext db,
            ViewRenderService viewRender,
            OperationsByInvoices operationsByInvoices,
            ExcelExportService excelExportService,
            PuppeteerPdfBrowserService puppeteerPdf,
            InvoiceQrService invoiceQrService,
            ILogger<InvoicesController> logger,
            ICurrentTenantService currentTenantService)
        {
            _db = db;
            _viewRender = viewRender;
            _operationsByInvoices = operationsByInvoices;
            _excelExportService = excelExportService;
            _puppeteerPdf = puppeteerPdf;
            _invoiceQrService = invoiceQrService;
            _logger = logger;
            _currentTenantService = currentTenantService;
        }

        private string? GetOrganizationId()
        {
            return HttpContext.Session.GetString("OrganizationId");
        }

        private string? GetUserId()
        {
            return HttpContext.Session.GetString("UserId");
        }

        private bool IsDetsadProfile()
        {
            var tenant = _currentTenantService.GetCurrent();
            return string.Equals(tenant.Profile.Key, "detsad", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolvePeriodicityForListing(string periodicity)
        {
            if (IsDetsadProfile())
                return "monthly";
            return periodicity ?? "";
        }

        private static IQueryable<Invoice> ApplyPeriodicityFilter(IQueryable<Invoice> query, string periodicity)
        {
            if (!string.IsNullOrWhiteSpace(periodicity))
                query = query.Where(i => i.Periodicity == periodicity);
            return query;
        }

        private async Task PopulateInvoiceCreateViewBagsAsync(string organizationId, bool includeGroups, CancellationToken cancellationToken = default)
        {
            if (includeGroups)
            {
                var groups = await _db.OrgClientGroups
                    .Where(g => g.OrganizationId == organizationId && g.IsDeleted == 0)
                    .OrderBy(g => g.Name)
                    .Select(g => new { g.Id, g.Name })
                    .ToListAsync(cancellationToken);

                ViewBag.OrgClientGroups = groups;
            }

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName, c.OrgClientGroupId })
                .ToListAsync(cancellationToken);

            var orgServices = await _db.OrganizationServices
                .Where(s => s.Organization == organizationId && (s.Isdeleted == null || s.Isdeleted == 0))
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.ServiceSumm, s.MinSumm, s.MaxSumm })
                .ToListAsync(cancellationToken);

            var settings = await _db.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);

            ViewBag.OrganizationClients = clients;
            ViewBag.OrganizationServices = orgServices;
            ViewBag.DetsadInvoiceServicesJson = JsonSerializer.Serialize(
                orgServices.Select(s => new { id = s.Id, name = s.Name ?? "", price = s.ServiceSumm ?? 0m }));
            ViewBag.UseDetsadInvoiceComposer = IsDetsadProfile();
            ViewBag.DisableInvoiceServiceSelection = settings?.DisableInvoiceServiceSelection ?? false;
            if (IsDetsadProfile())
            {
                ViewBag.AllowedHassameaccount = true;
                ViewBag.InvoicePayCodeMode = "duplicate_only";
            }
            else
            {
                ViewBag.AllowedHassameaccount = settings?.AllowedHassameaccount ?? false;
                ViewBag.InvoicePayCodeMode = !string.IsNullOrEmpty(settings?.InvoicePayCodeMode)
                    ? settings.InvoicePayCodeMode
                    : (settings?.AllowedHassameaccount == true ? "both" : "new_only");
            }
        }

        private async Task<OneTimePaymentPreviewVm> BuildOneTimePaymentPreviewAsync(
            CreateOneTimePaymentRequest request,
            string organizationId,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new InvalidOperationException("Данные платежа не переданы.");

            if (string.IsNullOrWhiteSpace(request.ClientId))
                throw new InvalidOperationException("Выберите клиента.");

            var client = await _db.OrganizationClients
                .Include(c => c.OrganizationNavigation)
                .FirstOrDefaultAsync(c => c.Id == request.ClientId &&
                                          c.Organization == organizationId &&
                                          c.ClientStatus == 1,
                    cancellationToken);

            if (client == null)
                throw new InvalidOperationException("Клиент не найден.");

            var paymentAt = request.PaymentAt ?? ParsersHelper.NowForTimestamp();
            var normalizedItems = (request.ServiceItems ?? new List<CreateInvoiceServiceItem>())
                .Where(x => !string.IsNullOrWhiteSpace(x.ServiceId) && (x.Qty ?? 0) > 0)
                .ToList();

            var manualAmountSom = request.ManualAmountSom ?? 0m;
            if (manualAmountSom < 0)
                throw new InvalidOperationException("Сумма не может быть отрицательной.");

            var lineVms = new List<OneTimePaymentPreviewLineVm>();
            if (normalizedItems.Count > 0)
            {
                var serviceIds = normalizedItems
                    .Select(x => x.ServiceId!)
                    .Distinct()
                    .ToList();

                var services = await _db.OrganizationServices
                    .Where(s => s.Organization == organizationId &&
                                serviceIds.Contains(s.Id) &&
                                (s.Isdeleted == null || s.Isdeleted == 0))
                    .ToDictionaryAsync(s => s.Id, s => s, cancellationToken);

                foreach (var item in normalizedItems)
                {
                    if (!services.TryGetValue(item.ServiceId!, out var service))
                        throw new InvalidOperationException("Одна из выбранных услуг не найдена.");

                    var qty = Math.Max(1, item.Qty ?? 1);
                    var unitPriceSom = service.ServiceSumm ?? 0;
                    var lineTotalSom = unitPriceSom * qty;
                    lineVms.Add(new OneTimePaymentPreviewLineVm
                    {
                        OrganizationServiceId = service.Id,
                        ServiceName = service.Name ?? "Услуга",
                        Quantity = qty,
                        UnitPriceSom = unitPriceSom,
                        LineTotalSom = lineTotalSom
                    });
                }
            }
            else if (manualAmountSom > 0)
            {
                lineVms.Add(new OneTimePaymentPreviewLineVm
                {
                    ServiceName = string.IsNullOrWhiteSpace(request.InvoiceName) ? "Разовый платёж" : request.InvoiceName.Trim(),
                    Quantity = 1,
                    UnitPriceSom = manualAmountSom,
                    LineTotalSom = manualAmountSom
                });
            }
            else
            {
                throw new InvalidOperationException("Укажите сумму или выберите хотя бы одну услугу.");
            }

            var totalSom = lineVms.Sum(x => x.LineTotalSom);
            if (totalSom <= 0)
                throw new InvalidOperationException("Сумма платежа должна быть больше нуля.");

            var payCode = string.IsNullOrWhiteSpace(request.PayCode)
                ? await _operationsByInvoices.GenerateNextPayCodeAsync(organizationId, cancellationToken)
                : request.PayCode.Trim();

            request.PayCode = payCode;
            request.PaymentAt = paymentAt;
            request.ServiceItems = normalizedItems;

            return new OneTimePaymentPreviewVm
            {
                Request = request,
                ClientName = client.ClientName ?? "Клиент",
                OrganizationName = client.OrganizationNavigation?.Name ?? "",
                PayCode = payCode,
                PaymentAt = paymentAt,
                TotalSom = totalSom,
                Lines = lineVms
            };
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
                return RedirectToAction("Login", "Account");

            if (string.Equals(_currentTenantService.GetCurrent().Profile.Key, "medclinic", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.Transactions)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);

            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(i => i.Client == clientId);

            periodicity = ResolvePeriodicityForListing(periodicity);
            query = ApplyPeriodicityFilter(query, periodicity);

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
            ViewBag.HidePeriodicityFilter = IsDetsadProfile();
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
                return RedirectToAction("Login", "Account");

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);
            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(i => i.Client == clientId);
            periodicity = ResolvePeriodicityForListing(periodicity);
            query = ApplyPeriodicityFilter(query, periodicity);
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

            ws.Cell(1, 1).Value = "Название счета";
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
                return RedirectToAction("Login", "Account");

            var query = _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoicePayments)
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (!string.IsNullOrWhiteSpace(statusFilter))
                query = query.Where(i => i.InvoiceStatus == statusFilter);
            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(i => i.Client == clientId);
            periodicity = ResolvePeriodicityForListing(periodicity);
            query = ApplyPeriodicityFilter(query, periodicity);
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

            var rows = new List<InvoicePaymentExcelRow>();
            foreach (var inv in list)
            {
                var payments = (inv.InvoicePayments ?? new List<InvoicePayment>())
                    .OrderBy(p => p.DateFrom ?? DateTime.MinValue)
                    .ToList();

                if (payments.Count == 0)
                {
                    rows.Add(new InvoicePaymentExcelRow
                    {
                        InvoiceId = inv.Id,
                        PayCode = inv.PayCode,
                        ClientName = inv.ClientNavigation?.ClientName ?? inv.Client,
                        PaymentStatus = "Нет записей"
                    });
                    continue;
                }

                foreach (var ip in payments)
                {
                    rows.Add(new InvoicePaymentExcelRow
                    {
                        InvoiceId = inv.Id,
                        PayCode = inv.PayCode,
                        ClientName = inv.ClientNavigation?.ClientName ?? inv.Client,
                        PeriodFrom = ip.DateFrom,
                        PeriodTo = ip.DateTo,
                        PeriodValue = ip.PeriodValue,
                        AmountTyiyn = ip.PaymentSumm,
                        PaymentStatus = ip.PaymentStatus
                    });
                }
            }

            var bytes = _excelExportService.ExportInvoicePaymentsAll(rows);
            var fileName = $"Platezhi_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> ExportInvoicePaymentsExcel(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoicePayments)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (invoice == null)
                return NotFound();

            var payments = (invoice.InvoicePayments ?? new List<InvoicePayment>()).OrderBy(p => p.DateFrom ?? DateTime.MinValue).ToList();

            var rows = payments.Select(ip => new InvoicePaymentExcelRow
            {
                PeriodFrom = ip.DateFrom,
                PeriodTo = ip.DateTo,
                PeriodValue = ip.PeriodValue,
                AmountTyiyn = ip.PaymentSumm,
                PaymentStatus = ip.PaymentStatus
            }).ToList();

            var bytes = _excelExportService.ExportInvoicePaymentsSingle(rows);
            var safeName = (invoice.PayCode ?? invoice.Id ?? "invoice").Replace(" ", "_");
            var fileName = $"Platezhi_{safeName}_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [RequirePermission("invoices.view")]
        public async Task<IActionResult> Details(string id, string? returnUrl = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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
                return RedirectToAction("Login", "Account");

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
            var activeQr = await _invoiceQrService.GetActiveQrAsync(invoice.Id, HttpContext.RequestAborted);
            ViewBag.QrImageDataUrl = !string.IsNullOrWhiteSpace(activeQr?.QrCodeBase64)
                ? $"data:image/png;base64,{activeQr!.QrCodeBase64}"
                : null;
            ViewBag.QrLink = activeQr?.QrLink;

            var pdfViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<Invoice>(ViewData)
            {
                Model = invoice
            };
            pdfViewData["ReceiverName"] = ViewBag.ReceiverName;
            pdfViewData["Inn"] = ViewBag.Inn;
            pdfViewData["Bank"] = ViewBag.Bank;
            pdfViewData["Account"] = ViewBag.Account;
            pdfViewData["QrImageDataUrl"] = ViewBag.QrImageDataUrl;
            pdfViewData["QrLink"] = ViewBag.QrLink;

            var html = await _viewRender.RenderViewToStringAsync(ControllerContext, "InvoicePdf", invoice, pdfViewData);
            var pdfBytes = await _puppeteerPdf.RenderPdfFromHtmlAsync(html, HttpContext.RequestAborted);

            var fileName = $"invoice_{(invoice.PayCode ?? invoice.Id ?? "invoice").Replace(" ", "_")}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> DownloadReceiptPdf(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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
            var pdfBytes = await _puppeteerPdf.RenderPdfFromHtmlAsync(html, HttpContext.RequestAborted);

            var fileName = $"receipt_{(payment.PeriodValue ?? payment.Id ?? "payment").Replace(" ", "_")}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> DownloadStatementPdf(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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
                var pdfBytes = await _puppeteerPdf.RenderPdfFromHtmlAsync(html, HttpContext.RequestAborted);

                var fileName = $"statement_{(invoice.PayCode ?? invoice.Id ?? "invoice").Replace(" ", "_")}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception)
            {
                TempData["Error"] = "Не удалось сформировать выписку в PDF. Попробуйте позже.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [RequirePermission("invoices.create")]
        [HttpGet]
        public async Task<IActionResult> Create(string? clientId = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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

        [RequirePermission("invoices.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice model)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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

        [RequirePermission("invoices.create")]
        [HttpGet]
        public async Task<IActionResult> Edit(string id, string? returnUrl = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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

        [RequirePermission("invoices.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Invoice model, string? returnUrl = null, string? serviceItemsJson = null, decimal? manualServicePriceSom = null, bool? useExistingPayCode = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

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
                List<UpdateInvoiceServiceItemDto>? serviceItems = null;
                if (!string.IsNullOrWhiteSpace(serviceItemsJson))
                {
                    try
                    {
                        var items = JsonSerializer.Deserialize<List<EditServiceItemDto>>(serviceItemsJson);
                        serviceItems = items?.Select(x => new UpdateInvoiceServiceItemDto { ServiceId = x.ServiceId, Qty = x.Qty }).ToList();
                    }
                    catch { }
                }
                var updateRequest = new UpdateInvoiceRequest
                {
                    Id = invoice.Id,
                    NameInvoice = model.NameInvoice,
                    Client = model.Client,
                    InvoiceStatus = model.InvoiceStatus,
                    Periodicity = model.Periodicity,
                    DateStartInvoice = model.DateStartInvoice,
                    DateEndInvoice = model.DateEndInvoice,
                    AutoProlongation = model.AutoProlongation ?? false,
                    NextStartInvoice = model.NextStartInvoice,
                    PayCode = model.PayCode,
                    Hassameaccount = useExistingPayCode == true,
                    ManualServicePriceSom = manualServicePriceSom,
                    ServiceItems = serviceItems
                };
                await _operationsByInvoices.ApplyInvoiceUpdateAsync(invoice, updateRequest, organizationId);
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
        [RequirePermission("invoices.create")]
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

            await _operationsByInvoices.ApplyInvoiceUpdateAsync(invoice, request, organizationId);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Изменения сохранены." });
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> CreateInvoicePartial()
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();
            await PopulateInvoiceCreateViewBagsAsync(organizationId, includeGroups: true, HttpContext.RequestAborted);

            return PartialView("~/Views/Clients/_CreateInvoicePartial.cshtml");
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> CreateOneTimePaymentPartial()
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            await PopulateInvoiceCreateViewBagsAsync(organizationId, includeGroups: false, HttpContext.RequestAborted);
            return PartialView("~/Views/Invoices/_CreateOneTimePaymentPartial.cshtml");
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetEditInvoicePartial([FromQuery] string invoiceId)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            if (string.IsNullOrEmpty(invoiceId))
                return BadRequest();

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoiceServices)
                    .ThenInclude(s => s.ServiceNavigation)
                .Include(i => i.InvoicePayments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
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

            var settings = await _db.OrganizationSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", invoice.Client);
            ViewBag.OrganizationServices = orgServices;
            ViewBag.DisableInvoiceServiceSelection = settings?.DisableInvoiceServiceSelection ?? false;
            ViewBag.AllowedHassameaccount = settings?.AllowedHassameaccount ?? false;
            ViewBag.InvoicePayCodeMode = !string.IsNullOrEmpty(settings?.InvoicePayCodeMode)
                ? settings.InvoicePayCodeMode
                : (settings?.AllowedHassameaccount == true ? "both" : "new_only");

            return PartialView("~/Views/Clients/_EditInvoicePartial.cshtml", invoice);
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetInvoicePayCodeOptions([FromQuery] string clientIds)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var ids = (clientIds ?? "")
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return Json(new { payCodeOptions = Array.Empty<object>() });

            var listQuery = _db.Invoices
                .Where(i => i.ClientNavigation != null &&
                            i.ClientNavigation.Organization == organizationId &&
                            i.Client != null &&
                            ids.Contains(i.Client) &&
                            i.PayCode != null &&
                            i.PayCode.Length > 0);

            if (!IsDetsadProfile())
                listQuery = listQuery.Where(i => i.Hassameaccount);

            var list = await listQuery
                .Select(i => new { i.PayCode, i.Id, i.NameInvoice })
                .ToListAsync();

            var distinctPayCodes = list
                .GroupBy(x => x.PayCode)
                .Select(g => new { payCode = g.Key, invoiceId = g.First().Id, nameInvoice = g.First().NameInvoice })
                .ToList();

            return Json(new { payCodeOptions = distinctPayCodes });
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetOrganizationHassamePayCodeOptions()
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var list = await _db.Invoices
                .Where(i => i.ClientNavigation != null &&
                            i.ClientNavigation.Organization == organizationId &&
                            i.Hassameaccount &&
                            i.PayCode != null &&
                            i.PayCode.Length > 0)
                .Select(i => new { i.PayCode, i.NameInvoice })
                .ToListAsync();

            var distinctPayCodes = list
                .GroupBy(x => x.PayCode)
                .Select(g => new { payCode = g.Key, nameInvoice = g.First().NameInvoice })
                .ToList();

            return Json(new { payCodeOptions = distinctPayCodes });
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetNextInvoiceNumber()
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            return Json(new { payCode = await _operationsByInvoices.GenerateNextPayCodeAsync(organizationId, HttpContext.RequestAborted) });
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetInvoicesInfo(string clientId, string? invoiceId = null)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

            if (client == null)
                return NotFound();

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

            var selectedInvoice = invoiceId != null
                ? invoices.FirstOrDefault(i => i.Id == invoiceId) ?? invoices.First()
                : invoices.First();

            var invoicePayments = await _db.InvoicePayments
                .Where(ip => ip.Invoice == selectedInvoice.Id)
                .OrderByDescending(ip => ip.DateFrom)
                .ToListAsync();

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
                    nameInvoice = selectedInvoice.NameInvoice,
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
                    paymentSumm = ip.PaymentSumm.HasValue ? ip.PaymentSumm.Value / 100m : (decimal?)null,
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

        [RequirePermission("invoices.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvoices([FromBody] CreateInvoicesRequest request)
        {
            var organizationId = GetOrganizationId();
            var userId = GetUserId();
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (request?.ClientIds == null || request.ClientIds.Count == 0)
                return BadRequest(new { success = false, message = "Выберите хотя бы одного получателя (группу или клиентов)." });

            var useManualService = request.ManualServicePriceSom.HasValue;
            if (!useManualService && (request.ServiceItems == null || request.ServiceItems.Count == 0))
                return BadRequest(new { success = false, message = "Выберите хотя бы одну услугу или укажите цену (режим «услуга по счёту»)." });

            if (useManualService && request.ManualServicePriceSom.GetValueOrDefault() < 0)
                return BadRequest(new { success = false, message = "Цена не может быть отрицательной." });

            if (!request.AutoProlongation && !request.DateEndInvoice.HasValue)
                return BadRequest(new { success = false, message = "Укажите дату конца счёта или включите автопролонгацию." });

            var clientIds = request.ClientIds.Distinct().ToList();
            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && clientIds.Contains(c.Id))
                .ToListAsync();
            if (clients.Count == 0)
                return BadRequest(new { success = false, message = "Выбранные клиенты не найдены." });

            Dictionary<string, OrganizationService>? orgServices = null;
            if (!useManualService)
            {
                var serviceIds = request.ServiceItems!.Select(x => x.ServiceId).Distinct().ToList();
                orgServices = await _db.OrganizationServices
                    .Where(s => s.Organization == organizationId && serviceIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s);
                if (orgServices.Count == 0)
                    return BadRequest(new { success = false, message = "Выбранные услуги не найдены." });
            }

            var input = new CreateInvoicesInput
            {
                OrganizationId = organizationId,
                UserId = userId,
                Clients = clients,
                NameInvoice = request.NameInvoice,
                DateStartInvoice = request.DateStartInvoice,
                DateEndInvoice = request.DateEndInvoice,
                Periodicity = request.Periodicity,
                AutoProlongation = request.AutoProlongation,
                UseCurrentDateTime = request.UseCurrentDateTime,
                UseManualService = useManualService,
                ManualServicePriceSom = request.ManualServicePriceSom,
                ServiceItems = request.ServiceItems?.Select(x => new CreateInvoiceServiceItemInput
                {
                    ServiceId = x.ServiceId,
                    Qty = x.Qty
                }).ToList(),
                OrgServices = orgServices,
                PayCode = request.PayCode,
                Hassameaccount = IsDetsadProfile() || request.Hassameaccount,
                ReuseClientPayCodeWhenExists = IsDetsadProfile()
            };

            try
            {
                var createdIds = await _operationsByInvoices.CreateInvoicesAsync(input);
                _logger.LogInformation(
                    "Invoices created. Count={Count} Clients={ClientCount} Org={OrgId} User={UserId} Ids={Ids}",
                    createdIds?.Count ?? 0,
                    clients.Count,
                    organizationId,
                    userId,
                    createdIds == null ? "" : string.Join(",", createdIds));
                return Json(new { success = true, message = "Счета созданы.", createdIds });
            }
            catch (DbUpdateException ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "CreateInvoices DB error. Org={OrgId} User={UserId}", organizationId, userId);
                return new JsonResult(new { success = false, message = "Ошибка БД: " + message }) { StatusCode = 500 };
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "CreateInvoices failed. Org={OrgId} User={UserId}", organizationId, userId);
                return new JsonResult(new { success = false, message = "Ошибка: " + message }) { StatusCode = 500 };
            }
        }

        [RequirePermission("invoices.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewOneTimePayment(CreateOneTimePaymentRequest request)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            try
            {
                var preview = await BuildOneTimePaymentPreviewAsync(request, organizationId, HttpContext.RequestAborted);
                return PartialView("~/Views/Invoices/_OneTimePaymentStepPartial.cshtml", preview);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 400;
                return Content(ex.Message);
            }
        }

        [RequirePermission("invoices.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOneTimePayment(CreateOneTimePaymentRequest request)
        {
            var organizationId = GetOrganizationId();
            var userId = GetUserId();
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
                return Unauthorized();

            OneTimePaymentPreviewVm preview;
            try
            {
                preview = await BuildOneTimePaymentPreviewAsync(request, organizationId, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 400;
                return Content(ex.Message);
            }

            try
            {
                var createResult = await _operationsByInvoices.CreateOneTimeInvoiceAsync(new CreateOneTimeInvoiceInput
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    ClientId = preview.Request.ClientId!,
                    FixedSumm = decimal.Round(preview.TotalSom * 100m, 0),
                    InvoiceName = preview.Request.InvoiceName,
                    PayCode = preview.PayCode,
                    DateStartInvoice = preview.PaymentAt,
                    DateEndInvoice = preview.PaymentAt,
                    // Для разового счёта сумма должна жить в графике платежа, а не в авансовом балансе.
                    Balance = 0,
                    Hassameaccount = preview.Request.Hassameaccount,
                    PaymentDateFrom = preview.PaymentAt,
                    PaymentDateTo = preview.PaymentAt,
                    PaymentPeriodValue = preview.PaymentAt.ToString("dd.MM.yyyy HH:mm"),
                    PaymentSumm = decimal.Round(preview.TotalSom * 100m, 0),
                    ServiceLines = preview.Lines
                        .Where(x => !string.IsNullOrWhiteSpace(x.OrganizationServiceId))
                        .Select(x => new CreateOneTimePaymentServiceLineInput
                        {
                            OrganizationServiceId = x.OrganizationServiceId,
                            ServiceSumm = decimal.Round(x.LineTotalSom * 100m, 0)
                        })
                        .ToList()
                }, HttpContext.RequestAborted);

                var invoice = await _db.Invoices
                    .Include(i => i.ClientNavigation)
                    .FirstOrDefaultAsync(i => i.Id == createResult.InvoiceId, HttpContext.RequestAborted);

                if (invoice == null)
                    throw new InvalidOperationException("Созданный счёт не найден.");

                var qr = await _invoiceQrService.GetActiveQrAsync(invoice.Id, HttpContext.RequestAborted);

                preview.IsCreated = true;
                preview.InvoiceId = invoice.Id;
                preview.DownloadPdfUrl = Url.Action(nameof(DownloadPdf), new { id = invoice.Id });
                preview.Qr = qr != null ? InvoiceQrService.ToDto(qr) : null;

                return PartialView("~/Views/Invoices/_OneTimePaymentStepPartial.cshtml", preview);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 400;
                return Content(ex.Message);
            }
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> OneTimePaymentResult(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account");

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .ThenInclude(c => c!.OrganizationNavigation)
                .Include(i => i.InvoiceServices)
                .ThenInclude(s => s.ServiceNavigation)
                .FirstOrDefaultAsync(i => i.Id == id &&
                                          i.ClientNavigation != null &&
                                          i.ClientNavigation.Organization == organizationId,
                    HttpContext.RequestAborted);

            if (invoice == null)
                return NotFound();

            var qr = await _invoiceQrService.GetActiveQrAsync(invoice.Id, HttpContext.RequestAborted);
            var lines = new List<OneTimePaymentPreviewLineVm>();
            if (invoice.InvoiceServices.Any())
            {
                foreach (var line in invoice.InvoiceServices)
                {
                    var lineSom = (line.ServiceSumm ?? 0) / 100m;
                    lines.Add(new OneTimePaymentPreviewLineVm
                    {
                        ServiceName = line.ServiceNavigation?.Name ?? invoice.NameInvoice ?? "Услуга",
                        Quantity = 1,
                        UnitPriceSom = lineSom,
                        LineTotalSom = lineSom,
                        OrganizationServiceId = line.Service
                    });
                }
            }
            else
            {
                var totalSom = (invoice.FixedSumm ?? 0) / 100m;
                lines.Add(new OneTimePaymentPreviewLineVm
                {
                    ServiceName = invoice.NameInvoice ?? "Разовый платёж",
                    Quantity = 1,
                    UnitPriceSom = totalSom,
                    LineTotalSom = totalSom
                });
            }

            var viewModel = new OneTimePaymentPreviewVm
            {
                ClientName = invoice.ClientNavigation?.ClientName ?? "Клиент",
                OrganizationName = invoice.ClientNavigation?.OrganizationNavigation?.Name ?? "",
                PayCode = invoice.PayCode ?? "",
                PaymentAt = invoice.DateStartInvoice ?? invoice.DateCreated ?? ParsersHelper.NowForTimestamp(),
                TotalSom = (invoice.FixedSumm ?? 0) / 100m,
                Lines = lines,
                IsCreated = true,
                InvoiceId = invoice.Id,
                DownloadPdfUrl = Url.Action(nameof(DownloadPdf), new { id = invoice.Id }),
                Qr = qr != null ? InvoiceQrService.ToDto(qr) : null
            };

            return View("~/Views/Invoices/ConfirmOneTimePayment.cshtml", viewModel);
        }

        [RequirePermission("invoices.create")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteInvoice([FromBody] DeleteInvoiceRequest? request)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var invoiceId = request?.InvoiceId?.Trim();
            if (string.IsNullOrEmpty(invoiceId))
                return Json(new { success = false, message = "Не указан счёт." });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);

            if (invoice == null)
                return Json(new { success = false, message = "Счёт не найден." });

            if (string.Equals(invoice.InvoiceStatus, "closed", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = true, message = "Счёт уже закрыт." });

            var payCode = invoice.PayCode?.Trim();
            invoice.InvoiceStatus = "closed";
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            if (!string.IsNullOrWhiteSpace(payCode))
            {
                var anchorId = await _db.Invoices
                    .AsNoTracking()
                    .Where(i => i.PayCode == payCode
                        && i.InvoiceStatus == "actual"
                        && i.ClientNavigation != null
                        && i.ClientNavigation.Organization == organizationId)
                    .OrderByDescending(i => i.DateCreated)
                    .Select(i => i.Id)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);

                if (!string.IsNullOrEmpty(anchorId))
                    _invoiceQrService.EnqueueRefreshForPayCode(payCode, anchorId);
                else
                    await _invoiceQrService.DisableActiveQrAsync(invoiceId, HttpContext.RequestAborted);
            }

            _logger.LogInformation("Invoice closed (deleted). InvoiceId={InvoiceId} PayCode={PayCode}", invoiceId, payCode);
            return Json(new { success = true, message = "Счёт удалён." });
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetActiveQr(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var invoiceExists = await _db.Invoices
                .AnyAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (!invoiceExists)
                return Json(new { success = false, message = "Счёт не найден." });

            var activeQr = await _invoiceQrService.GetActiveQrAsync(id, HttpContext.RequestAborted);
            if (activeQr == null)
                return Json(new { success = true, qr = (InvoiceQrDto?)null });

            return Json(new { success = true, qr = InvoiceQrService.ToDto(activeQr) });
        }

        [RequirePermission("invoices.view")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GenerateQr([FromBody] GenerateInvoiceQrRequest? request, string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var invoice = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Include(i => i.InvoicePayments)
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (invoice == null)
                return Json(new { success = false, message = "Счёт не найден." });

            if (string.IsNullOrWhiteSpace(invoice.PayCode))
                return Json(new { success = false, message = "У счёта отсутствует PayCode для генерации QR." });

            try
            {
                var qr = await _invoiceQrService.GenerateForInvoiceAsync(
                    invoice,
                    request?.PurchaseSumSom,
                    HttpContext.RequestAborted);

                return Json(new
                {
                    success = true,
                    message = "QR-код успешно сгенерирован.",
                    qr = InvoiceQrService.ToDto(qr)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [RequirePermission("invoices.view")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DisableQr(string id)
        {
            var organizationId = GetOrganizationId();
            if (string.IsNullOrEmpty(organizationId))
                return Json(new { success = false, message = "Не авторизован." });

            var invoiceExists = await _db.Invoices
                .AnyAsync(i => i.Id == id && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId);
            if (!invoiceExists)
                return Json(new { success = false, message = "Счёт не найден." });

            var qr = await _invoiceQrService.DisableActiveQrAsync(id, HttpContext.RequestAborted);
            if (qr == null)
                return Json(new { success = false, message = "Активный QR-код не найден." });

            return Json(new
            {
                success = true,
                message = "QR-код выключен.",
                qr = InvoiceQrService.ToDto(qr)
            });
        }

        private static string GetPeriodicityText(string? periodicity)
        {
            return periodicity switch
            {
                "daily" => "Ежедневно",
                "weekly" => "Еженедельно",
                "monthly" => "Ежемесячно",
                "yearly" => "Ежегодно",
                "oneTime" => "Одноразовый",
                "any" => "Прием в любой момент",
                _ => periodicity != null && int.TryParse(periodicity, out var days)
                    ? $"Каждые {days} дней"
                    : periodicity ?? "Не указано"
            };
        }
    }
}
