using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using WebApplication1.Services;
using WebApplication1.Dtos;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
    [RequireAuth]
    public class CabinetController : Controller
    {
        private readonly AppDbContext _db;
        private readonly OperationsByInvoices _operationsByInvoices;
        private readonly ClientService _clientService;
        private readonly ExcelExportService _excelExportService;
        private const string ChildrenImportSessionPrefix = "__children_import:";

        public CabinetController(AppDbContext db, OperationsByInvoices operationsByInvoices, ClientService clientService, ExcelExportService excelExportService)
        {
            _db = db;
            _operationsByInvoices = operationsByInvoices;
            _clientService = clientService;
            _excelExportService = excelExportService;
        }

        [RequirePermission("children.create")]
        [HttpGet]
        public IActionResult DownloadChildrenImportTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Дети");
            ws.Cell(1, 1).Value = "ФИО";
            ws.Cell(1, 2).Value = "ИНН";
            ws.Cell(1, 3).Value = "Номер телефона";
            ws.Cell(1, 4).Value = "Адрес";
            ws.Cell(1, 5).Value = "Эл. почта";
            ws.Row(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var bytes = stream.ToArray();
            var fileName = $"children_import_template_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static string NormalizeImportHeader(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return s.Trim().ToLowerInvariant().Replace("ё", "е");
        }

        /// <summary>
        /// Сопоставляет колонки по первой строке (русские/англ. заголовки) или фиксированному порядку A–E.
        /// </summary>
        private static Dictionary<string, int> BuildChildrenImportColumnMap(IXLWorksheet ws)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var firstRow = ws.FirstRowUsed();
            if (firstRow == null) return map;
            var r = firstRow.RowNumber();
            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 5;
            for (var c = 1; c <= lastCol; c++)
            {
                var h = NormalizeImportHeader(ws.Cell(r, c).GetString());
                if (string.IsNullOrEmpty(h)) continue;

                if (!map.ContainsKey("fio") && (h == "фио" || h.Contains("фио")))
                    map["fio"] = c;
                else if (!map.ContainsKey("inn") && h.Contains("инн"))
                    map["inn"] = c;
                else if (!map.ContainsKey("phone") && (h.Contains("телефон") || h.Contains("номер")))
                    map["phone"] = c;
                else if (!map.ContainsKey("address") && h.Contains("адрес"))
                    map["address"] = c;
                else if (!map.ContainsKey("email") && (h.Contains("почт") || h.Contains("email") || h.Contains("e-mail") || h.Contains("эл")))
                    map["email"] = c;
            }

            return map;
        }

        private static int ImportCol(Dictionary<string, int> map, string key, int fallback) =>
            map.TryGetValue(key, out var col) ? col : fallback;

        private static string GetImportCellTrim(IXLWorksheet ws, int rowNum, int col)
        {
            if (col < 1 || rowNum < 1) return "";
            try
            {
                var v = ws.Cell(rowNum, col).GetString();
                return v?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static bool IsChildrenImportRowBlank(string fio, string inn, string phone, string addr, string email) =>
            string.IsNullOrWhiteSpace(fio) && string.IsNullOrWhiteSpace(inn) && string.IsNullOrWhiteSpace(phone) &&
            string.IsNullOrWhiteSpace(addr) && string.IsNullOrWhiteSpace(email);

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PreviewChildrenImport(IFormFile file)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Выберите файл Excel и повторите попытку.";
                TempData["OpenChildrenImportModal"] = true;
                return RedirectToAction(nameof(Children));
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                {
                    TempData["Error"] = "Не удалось прочитать лист Excel.";
                    TempData["OpenChildrenImportModal"] = true;
                    return RedirectToAction(nameof(Children));
                }

                var colMap = BuildChildrenImportColumnMap(ws);
                var colFio = ImportCol(colMap, "fio", 1);
                var colInn = ImportCol(colMap, "inn", 2);
                var colPhone = ImportCol(colMap, "phone", 3);
                var colAddr = ImportCol(colMap, "address", 4);
                var colEmail = ImportCol(colMap, "email", 5);

                var headerRow = ws.FirstRowUsed()?.RowNumber() ?? 1;
                var firstDataRow = headerRow + 1;
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
                var rows = new List<ChildrenImportRowDto>();

                for (var r = firstDataRow; r <= lastRow; r++)
                {
                    var fio = GetImportCellTrim(ws, r, colFio);
                    var inn = GetImportCellTrim(ws, r, colInn);
                    var phone = GetImportCellTrim(ws, r, colPhone);
                    var addr = GetImportCellTrim(ws, r, colAddr);
                    var email = GetImportCellTrim(ws, r, colEmail);

                    if (IsChildrenImportRowBlank(fio, inn, phone, addr, email))
                        continue;

                    var model = new ChildrenImportRowDto
                    {
                        ClientName = string.IsNullOrWhiteSpace(fio) ? null : fio,
                        ClientInn = string.IsNullOrWhiteSpace(inn) ? null : inn,
                        ClientPhone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        ClientAddress = string.IsNullOrWhiteSpace(addr) ? null : addr,
                        ClientEmail = string.IsNullOrWhiteSpace(email) ? null : email
                    };

                    var missing = new List<string>();
                    if (string.IsNullOrWhiteSpace(model.ClientName))
                        missing.Add("ФИО");
                    if (string.IsNullOrWhiteSpace(model.ClientInn))
                        missing.Add("ИНН");

                    if (missing.Count > 0)
                    {
                        model.IsValid = false;
                        model.Error = "Обязательно: " + string.Join(", ", missing);
                    }
                    else
                    {
                        model.IsValid = true;
                    }

                    rows.Add(model);
                }

                if (!rows.Any())
                {
                    TempData["Error"] = "В файле нет строк с данными (проверьте ФИО и ИНН в каждой строке).";
                    TempData["OpenChildrenImportModal"] = true;
                    return RedirectToAction(nameof(Children));
                }

                var importId = Guid.NewGuid().ToString("N");
                var sessionKey = ChildrenImportSessionPrefix + importId;
                HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(rows));
                TempData["ChildrenImportId"] = importId;
                TempData["OpenChildrenImportModal"] = true;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ошибка при разборе файла: {ex.Message}";
                TempData["OpenChildrenImportModal"] = true;
            }

            return RedirectToAction(nameof(Children));
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmChildrenImport(string importId)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(importId))
            {
                TempData["Error"] = "Не найден идентификатор импорта.";
                return RedirectToAction(nameof(Children));
            }

            var sessionKey = ChildrenImportSessionPrefix + importId;
            var json = HttpContext.Session.GetString(sessionKey);
            if (string.IsNullOrEmpty(json))
            {
                TempData["Error"] = "Данные импорта не найдены или устарели.";
                return RedirectToAction(nameof(Children));
            }

            List<ChildrenImportRowDto>? rows;
            try
            {
                rows = JsonSerializer.Deserialize<List<ChildrenImportRowDto>>(json) ?? new List<ChildrenImportRowDto>();
            }
            catch
            {
                TempData["Error"] = "Не удалось прочитать данные импорта.";
                return RedirectToAction(nameof(Children));
            }

            int imported = 0;
            int skipped = 0;

            foreach (var model in rows)
            {
                if (model == null || !model.IsValid || string.IsNullOrWhiteSpace(model.ClientName) ||
                    string.IsNullOrWhiteSpace(model.ClientInn))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var input = new CreateClientInput
                    {
                        ClientName = model.ClientName,
                        ClientInn = model.ClientInn.Trim(),
                        ClientPhone = string.IsNullOrWhiteSpace(model.ClientPhone) ? null : model.ClientPhone,
                        ClientAddress = string.IsNullOrWhiteSpace(model.ClientAddress) ? null : model.ClientAddress,
                        ClientEmail = string.IsNullOrWhiteSpace(model.ClientEmail) ? null : model.ClientEmail
                    };
                    await _clientService.CreateClientAsync(input, organizationId, userId);
                    imported++;
                }
                catch
                {
                    skipped++;
                }
            }

            HttpContext.Session.Remove(sessionKey);
            TempData["Success"] = $"Импорт завершён. Добавлено: {imported}, пропущено: {skipped}.";

            return RedirectToAction(nameof(Children));
        }

        [RequirePermission("dashboard.view")]
        public async Task<IActionResult> Index()
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var clientIds = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId)
                .Select(c => c.Id)
                .ToListAsync();

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .ToListAsync();

            var invoiceIds = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .Select(i => i.Id)
                .ToListAsync();

            var invoices = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .ToListAsync();

            var transactions = await _db.Transactions
                .Where(t => t.TransactionStatus == "success" &&
                            t.Invoice != null &&
                            invoiceIds.Contains(t.Invoice) &&
                            t.Summ.HasValue &&
                            t.TransactionType == "payPaymentInvoice")
                .ToListAsync();

            var now = DateTime.UtcNow;
            var today = DateTime.Today;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            var monthIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                           t.TransactionDate.Value.Month == currentMonth &&
                           t.TransactionDate.Value.Year == currentYear)
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

            var yearIncome = transactions
                .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == currentYear)
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

            var balanceByClient = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client) && i.InvoiceStatus == "actual")
                .GroupBy(i => i.Client!)
                .Select(g => new { ClientId = g.Key, TotalBalance = g.Sum(i => i.Balance ?? 0m) })
                .ToDictionaryAsync(x => x.ClientId, x => x.TotalBalance);

            var totalClients = clients.Count;
            var activeInvoicesCount = invoices.Count(i => i.InvoiceStatus == "actual");

            // Данные для графика: неделя (последние 7 дней), месяц (дни текущего месяца), год (12 месяцев)
            var chartWeek = new List<object>();
            for (var d = 6; d >= 0; d--)
            {
                var date = today.AddDays(-d);
                var sum = transactions
                    .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Date == date)
                    .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;
                chartWeek.Add(new { label = date.ToString("dd.MM"), value = sum });
            }

            var chartMonth = new List<object>();
            var firstDay = new DateTime(currentYear, currentMonth, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
            {
                var sum = transactions
                    .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Date == date)
                    .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;
                chartMonth.Add(new { label = date.ToString("dd.MM"), value = sum });
            }

            var chartYear = new List<object>();
            for (var m = 1; m <= 12; m++)
            {
                var sum = transactions
                    .Where(t => t.TransactionDate.HasValue &&
                                t.TransactionDate.Value.Month == m &&
                                t.TransactionDate.Value.Year == currentYear)
                    .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;
                chartYear.Add(new { label = new DateTime(currentYear, m, 1).ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")), value = sum });
            }

            // Организация и условия обслуживания
            var organization = await _db.Organizations.FindAsync(organizationId);
            var orgSettings = await _db.OrganizationSettings
                .Include(s => s.Commission)
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);
            var agentCommission = await _db.AgentCommissions
                .Include(ac => ac.Commission)
                .Include(ac => ac.LowerCommission)
                .FirstOrDefaultAsync(ac => ac.OrganizationId == organizationId);

            var commissionName = orgSettings?.Commission?.Name ?? agentCommission?.Commission?.Name;
            var commissionKind = orgSettings?.Commission?.CommissionKind ?? agentCommission?.Commission?.CommissionKind;
            var commissionRate = orgSettings?.Commission?.Rate ?? agentCommission?.Commission?.Rate;
            var commissionFixed = orgSettings?.Commission?.FixedAmount ?? agentCommission?.Commission?.FixedAmount;
            var billingType = orgSettings?.BillingType ?? "commission";
            var hasSubscription = string.Equals(billingType, "subscription", StringComparison.OrdinalIgnoreCase);

            // Должники: топ по сумме долга
            var debtorsList = clients
                .Where(c => balanceByClient.GetValueOrDefault(c.Id, 0m) < 0)
                .OrderBy(c => balanceByClient.GetValueOrDefault(c.Id, 0m))
                .Take(10)
                .Select(c => new
                {
                    ClientName = c.ClientName ?? "—",
                    ClientId = c.Id,
                    DebtAmount = Math.Abs(balanceByClient.GetValueOrDefault(c.Id, 0m)) / 100m,
                    Status = c.ClientStatus == 1 ? "Активный" : "Приостановлен"
                })
                .ToList();

            // Последние/актуальные счета
            var recentInvoices = invoices
                .Where(i => i.InvoiceStatus == "actual")
                .OrderByDescending(i => i.DateCreated ?? DateTime.MinValue)
                .Take(10)
                .Select(i => new
                {
                    Id = i.Id,
                    Name = i.NameInvoice ?? "Без названия",
                    Balance = (i.Balance ?? 0m) / 100m,
                    Status = i.InvoiceStatus == "actual" ? "Активный" : i.InvoiceStatus ?? "—",
                    ClientName = i.ClientNavigation?.ClientName
                })
                .ToList();

            ViewBag.MonthIncome = monthIncome;
            ViewBag.YearIncome = yearIncome;
            ViewBag.TotalClients = totalClients;
            ViewBag.ActiveInvoices = activeInvoicesCount;
            ViewBag.ChartWeek = System.Text.Json.JsonSerializer.Serialize(chartWeek);
            ViewBag.ChartMonth = System.Text.Json.JsonSerializer.Serialize(chartMonth);
            ViewBag.ChartYear = System.Text.Json.JsonSerializer.Serialize(chartYear);
            ViewBag.Organization = organization;
            ViewBag.OrgSettings = orgSettings;
            ViewBag.HasSubscription = hasSubscription;
            ViewBag.CommissionName = commissionName;
            ViewBag.CommissionKind = commissionKind;
            ViewBag.CommissionRate = commissionRate;
            ViewBag.CommissionFixed = commissionFixed;
            ViewBag.DebtorsList = debtorsList;
            ViewBag.RecentInvoices = recentInvoices;

            return View();
        }

        [HttpPost]
        [RequirePermission("children.create")]
        public async Task<IActionResult> CreateChild([FromBody] CreateChildRequest request)
        {
            try
            {
                var organizationId = HttpContext.Session.GetString("OrganizationId");
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var input = new CreateClientInput
                {
                    ClientName = request.ClientName,
                    ClientInn = request.ClientInn,
                    OrgClientGroupId = request.OrgClientGroupId,
                    ClientPhone = request.ClientPhone,
                    ClientAddress = request.ClientAdres,
                    ClientEmail = request.ClientEmail,
                    ClientWa = request.ClientWa,
                    ClientTg = request.ClientTg,
                    AdditionalFields = request.AdditionalFields
                };
                var clientId = await _clientService.CreateClientAsync(input, organizationId, userId);
                return Json(new { success = true, message = "Ребенок успешно добавлен", clientId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка при добавлении ребенка: {ex.Message}" });
            }
        }

        [RequirePermission("children.view")]
        public async Task<IActionResult> Children(string search = "", string statusFilter = "active", bool debtorsOnly = false)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var result = await _clientService.GetClientsForCabinetAsync(organizationId, search, statusFilter, debtorsOnly);

            var openChildrenImportModal = TempData["OpenChildrenImportModal"] != null;

            // Предпросмотр импорта детей из Excel
            if (TempData.ContainsKey("ChildrenImportId"))
            {
                var importId = TempData["ChildrenImportId"]?.ToString();
                if (!string.IsNullOrEmpty(importId))
                {
                    var sessionKey = ChildrenImportSessionPrefix + importId;
                    var json = HttpContext.Session.GetString(sessionKey);
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            var previewRows = JsonSerializer.Deserialize<List<ChildrenImportRowDto>>(json) ?? new List<ChildrenImportRowDto>();
                            ViewBag.ChildrenImportPreviewId = importId;
                            ViewBag.ChildrenImportPreviewRows = previewRows;
                            openChildrenImportModal = true;
                        }
                        catch
                        {
                            // если не удалось разобрать — quietly ignore, покажем ошибку только при подтверждении
                        }
                    }
                }
            }

            ViewBag.OpenChildrenImportModal = openChildrenImportModal;

            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DebtorsOnly = debtorsOnly;
            ViewBag.ChildrenData = result.ChildrenData;
            ViewBag.ClientTotalBalanceByClientId = result.ClientTotalBalanceByClientId;
            ViewBag.FirstInvoiceIdByClientId = result.FirstInvoiceIdByClientId;
            ViewBag.OrganizationFields = result.OrganizationFields;
            ViewBag.OrgClientGroups = result.OrgClientGroups;
            ViewBag.AllowedHassameaccount = result.AllowedHassameaccount;
            ViewBag.InvoicePayCodeMode = result.InvoicePayCodeMode;

            return View();
        }

        [RequirePermission("children.view")]
        public async Task<IActionResult> GetChildInfo(string clientId)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var info = await _clientService.GetClientInfoAsync(clientId, organizationId);
            if (info == null)
                return NotFound();

            return Json(new
            {
                client = new
                {
                    id = info.Id,
                    name = info.ClientName,
                    phone = info.ClientPhone,
                    email = info.ClientEmail,
                    address = info.ClientAddress,
                    inn = info.ClientInn,
                    balance = info.ClientBalance,
                    status = info.ClientStatus,
                    createdDate = info.CreatedDate,
                    updatedDate = info.UpdatedDate,
                    logo = info.ClientLogo,
                    orgClientGroupId = info.OrgClientGroupId,
                    clientWa = info.ClientWa,
                    clientTg = info.ClientTg
                },
                additionalFields = info.AdditionalFields.Select(af => new { af.FieldId, af.FieldName, af.FieldType, af.Value }).ToList()
            });
        }

        [RequirePermission("children.create")]
        [HttpPost]
        public async Task<IActionResult> UpdateChild([FromBody] UpdateChildRequest request)
        {
            try
            {
                var organizationId = HttpContext.Session.GetString("OrganizationId");
                if (string.IsNullOrEmpty(organizationId))
                    return Unauthorized();

                var input = new UpdateClientInput
                {
                    ClientId = request.ClientId,
                    ClientName = request.ClientName,
                    ClientInn = request.ClientInn,
                    OrgClientGroupId = request.OrgClientGroupId,
                    ClientPhone = request.ClientPhone,
                    ClientAddress = request.ClientAdres,
                    ClientEmail = request.ClientEmail,
                    ClientWa = request.ClientWa,
                    ClientTg = request.ClientTg,
                    ClientStatus = request.ClientStatus,
                    AdditionalFields = request.AdditionalFields
                };
                var updated = await _clientService.UpdateClientAsync(input, organizationId);
                if (!updated)
                    return Json(new { success = false, message = "Клиент не найден." });
                return Json(new { success = true, message = "Профиль обновлён." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [RequirePermission("invoices.view")]
        public async Task<IActionResult> GetInvoicesInfo(string clientId, string? invoiceId = null)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

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

        [RequirePermission("children.view")]
        public async Task<IActionResult> GetChildDetails(string clientId)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

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
                    address = client.ClientAddress,
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

        [RequirePermission("transactions.view")]
        public async Task<IActionResult> GetClientTransactions(string clientId, List<string>? agentIds = null)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

            if (client == null)
            {
                return NotFound();
            }

            var invoices = await _db.Invoices
                .Where(i => i.Client == clientId)
                .Select(i => i.Id)
                .ToListAsync();

            var query = _db.Transactions
                .Include(t => t.AgentNavigation)
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice));

            var selectedAgentIds = agentIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
                .Select(id => id!.Trim())
                .ToList() ?? new List<string>();
            if (selectedAgentIds.Count > 0)
                query = query.Where(t => t.Agent != null && selectedAgentIds.Contains(t.Agent));

            var transactions = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            var agentIdsForClient = await _db.Transactions
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice) && t.Agent != null)
                .Select(t => t.Agent)
                .Distinct()
                .ToListAsync();
            var agentsList = await _db.Agents
                .Where(a => agentIdsForClient.Contains(a.Id))
                .OrderBy(a => a.Name)
                .Select(a => new { id = a.Id, name = a.Name ?? a.Id })
                .ToListAsync();

            return Json(new
            {
                client = new
                {
                    name = client.ClientName
                },
                agents = agentsList,
                transactions = transactions.Select(t => new
                {
                    id = t.Id,
                    transactionDate = t.TransactionDate,
                    summ = t.Summ,
                    transactionSumm = t.TransactionSumm,
                    transactionType = t.TransactionType,
                    transactionStatus = t.TransactionStatus,
                    invoiceId = t.Invoice,
                    agentId = t.Agent,
                    agentName = t.AgentNavigation != null ? (t.AgentNavigation.Name ?? t.AgentNavigation.Id) : null
                }).ToList()
            });
        }

        [RequirePermission("transactions.view")]
        public async Task<IActionResult> Payments(string dateFrom = "", string dateTo = "", string search = "", string clientId = "", string statusFilter = "", List<string>? agentIds = null)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoices = await _db.Invoices
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
                .Select(i => i.Id)
                .ToListAsync();

            var query = _db.Transactions
                .Include(t => t.AgentNavigation)
                .Include(t => t.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice));

            if (DateTime.TryParse(dateFrom, out var fromDate))
                query = query.Where(t => t.TransactionDate >= fromDate.Date);
            if (DateTime.TryParse(dateTo, out var toDate))
                query = query.Where(t => t.TransactionDate != null && t.TransactionDate.Value.Date <= toDate.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(statusFilter) && (statusFilter == "success" || statusFilter == "error"))
                query = query.Where(t => t.TransactionStatus == statusFilter);

            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(t => t.InvoiceNavigation != null && t.InvoiceNavigation.Client == clientId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(t =>
                    (t.InvoiceNavigation != null && t.InvoiceNavigation.ClientNavigation != null && t.InvoiceNavigation.ClientNavigation.ClientName != null && t.InvoiceNavigation.ClientNavigation.ClientName.ToLower().Contains(term)) ||
                    (t.InvoiceNavigation != null && t.InvoiceNavigation.PayCode != null && t.InvoiceNavigation.PayCode.ToLower().Contains(term)) ||
                    (t.AgentNavigation != null && t.AgentNavigation.Name != null && t.AgentNavigation.Name.ToLower().Contains(term)));
            }

            var selectedAgentIds = agentIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
                .Select(id => id!.Trim())
                .ToList() ?? new List<string>();
            if (selectedAgentIds.Count > 0)
                query = query.Where(t => t.Agent != null && selectedAgentIds.Contains(t.Agent));

            var transactions = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            var agentIdsInOrg = await _db.Transactions
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice))
                .Select(t => t.Agent)
                .Where(a => a != null)
                .Distinct()
                .ToListAsync();
            var agentsList = await _db.Agents
                .Where(a => agentIdsInOrg.Contains(a.Id))
                .OrderBy(a => a.Name)
                .Select(a => new { a.Id, a.Name })
                .ToListAsync();

            var clientIdsInOrg = await _db.Invoices
                .Where(i => i.Client != null && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
                .Select(i => i.Client)
                .Distinct()
                .ToListAsync();
            var clientsList = await _db.OrganizationClients
                .Where(c => clientIdsInOrg.Contains(c.Id))
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();

            var clientsForDropdown = clientsList.Select(c => new { Id = c.Id, ClientName = c.ClientName ?? c.Id }).ToList();
            var selectedClientName = "";
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                var sel = clientsList.FirstOrDefault(c => c.Id == clientId);
                selectedClientName = sel?.ClientName ?? clientId;
            }

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.Search = search ?? "";
            ViewBag.ClientId = clientId ?? "";
            ViewBag.SelectedClientName = selectedClientName;
            ViewBag.StatusFilter = statusFilter ?? "";
            ViewBag.SelectedAgentIds = selectedAgentIds;
            ViewBag.AgentsList = agentsList;
            ViewBag.ClientsList = clientsList;
            ViewBag.Clients = clientsForDropdown;
            return View(transactions);
        }

        /// <summary>
        /// Выгрузка транзакций в Excel с теми же фильтрами, что и на странице «Транзакции».
        /// </summary>
        [RequirePermission("transactions.view")]
        [HttpGet]
        public async Task<IActionResult> ExportTransactionsExcel(string dateFrom = "", string dateTo = "", string search = "", string clientId = "", string statusFilter = "", List<string>? agentIds = null)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return RedirectToAction("Login", "Account", new { area = "" });

            var invoices = await _db.Invoices
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
                .Select(i => i.Id)
                .ToListAsync();

            var query = _db.Transactions
                .Include(t => t.AgentNavigation)
                .Include(t => t.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice));

            if (DateTime.TryParse(dateFrom, out var fromDate))
                query = query.Where(t => t.TransactionDate >= fromDate.Date);
            if (DateTime.TryParse(dateTo, out var toDate))
                query = query.Where(t => t.TransactionDate != null && t.TransactionDate.Value.Date <= toDate.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(statusFilter) && (statusFilter == "success" || statusFilter == "error"))
                query = query.Where(t => t.TransactionStatus == statusFilter);

            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(t => t.InvoiceNavigation != null && t.InvoiceNavigation.Client == clientId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(t =>
                    (t.InvoiceNavigation != null && t.InvoiceNavigation.ClientNavigation != null && t.InvoiceNavigation.ClientNavigation.ClientName != null && t.InvoiceNavigation.ClientNavigation.ClientName.ToLower().Contains(term)) ||
                    (t.InvoiceNavigation != null && t.InvoiceNavigation.PayCode != null && t.InvoiceNavigation.PayCode.ToLower().Contains(term)) ||
                    (t.AgentNavigation != null && t.AgentNavigation.Name != null && t.AgentNavigation.Name.ToLower().Contains(term)));
            }

            var selectedAgentIds = agentIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
                .Select(id => id!.Trim())
                .ToList() ?? new List<string>();
            if (selectedAgentIds.Count > 0)
                query = query.Where(t => t.Agent != null && selectedAgentIds.Contains(t.Agent));

            var list = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            var rows = list.Select(t => new TransactionExcelRow
            {
                Date = t.TransactionDate,
                ClientName = t.InvoiceNavigation?.ClientNavigation?.ClientName,
                PayCode = t.InvoiceNavigation?.PayCode,
                AgentName = t.AgentNavigation?.Name ?? t.Agent,
                AmountTyiyn = t.Summ,
                TotalTyiyn = t.TransactionSumm,
                Status = t.TransactionStatus
            }).ToList();

            var bytes = _excelExportService.ExportTransactions(rows);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "transactions.xlsx");
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> CreateInvoicePartial()
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var groups = await _db.OrgClientGroups
                .Where(g => g.OrganizationId == organizationId && g.IsDeleted == 0)
                .OrderBy(g => g.Name)
                .Select(g => new { g.Id, g.Name })
                .ToListAsync();

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName, c.OrgClientGroupId })
                .ToListAsync();

            var orgServices = await _db.OrganizationServices
                .Where(s => s.Organization == organizationId && (s.Isdeleted == null || s.Isdeleted == 0))
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name, s.ServiceSumm, s.MinSumm, s.MaxSumm })
                .ToListAsync();

            var settings = await _db.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

            ViewBag.OrgClientGroups = groups;
            ViewBag.OrganizationClients = clients;
            ViewBag.OrganizationServices = orgServices;
            ViewBag.DisableInvoiceServiceSelection = settings?.DisableInvoiceServiceSelection ?? false;
            ViewBag.AllowedHassameaccount = settings?.AllowedHassameaccount ?? false;
            ViewBag.InvoicePayCodeMode = !string.IsNullOrEmpty(settings?.InvoicePayCodeMode) ? settings.InvoicePayCodeMode : (settings?.AllowedHassameaccount == true ? "both" : "new_only");
            return PartialView("_CreateInvoicePartial");
        }

        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetEditInvoicePartial([FromQuery] string invoiceId)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
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

            var settings = await _db.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

            ViewBag.Clients = new SelectList(clients, "Id", "ClientName", invoice.Client);
            ViewBag.OrganizationServices = orgServices;
            ViewBag.DisableInvoiceServiceSelection = settings?.DisableInvoiceServiceSelection ?? false;
            ViewBag.AllowedHassameaccount = settings?.AllowedHassameaccount ?? false;
            ViewBag.InvoicePayCodeMode = !string.IsNullOrEmpty(settings?.InvoicePayCodeMode) ? settings.InvoicePayCodeMode : (settings?.AllowedHassameaccount == true ? "both" : "new_only");
            return PartialView("_EditInvoicePartial", invoice);
        }

        /// <summary>
        /// Для настройки AllowedHassameaccount: возвращает список лицевых счетов (PayCode) из счетов с Hassameaccount=true по выбранным клиентам.
        /// </summary>
        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetInvoicePayCodeOptions([FromQuery] string clientIds)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var ids = (clientIds ?? "")
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
                return Json(new { payCodeOptions = Array.Empty<object>() });

            var list = await _db.Invoices
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId
                    && ids.Contains(i.Client)
                    && i.Hassameaccount
                    && i.PayCode != null && i.PayCode.Length > 0)
                .Select(i => new { i.PayCode, i.Id, i.NameInvoice })
                .ToListAsync();

            var distinctPayCodes = list
                .GroupBy(x => x.PayCode)
                .Select(g => new { payCode = g.Key, invoiceId = g.First().Id, nameInvoice = g.First().NameInvoice })
                .ToList();

            return Json(new { payCodeOptions = distinctPayCodes });
        }

        /// <summary>
        /// Для шага «добавить ребёнка + счёт»: список лицевых счетов с Hassameaccount=true по организации (для привязки нового счёта к общему).
        /// </summary>
        [RequirePermission("invoices.view")]
        [HttpGet]
        public async Task<IActionResult> GetOrganizationHassamePayCodeOptions()
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var list = await _db.Invoices
                .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId
                    && i.Hassameaccount && i.PayCode != null && i.PayCode.Length > 0)
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
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
                return Unauthorized();

            var orgPrefix = organizationId;
            var prefixLen = orgPrefix.Length;
            var clientIds = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId)
                .Select(c => c.Id)
                .ToListAsync();
            var payCodes = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client) && i.PayCode != null && i.PayCode.Length == prefixLen + 9 && i.PayCode.StartsWith(orgPrefix))
                .Select(i => i.PayCode)
                .ToListAsync();
            long maxCounter = 0;
            foreach (var pc in payCodes)
            {
                if (pc != null && pc.Length > prefixLen && long.TryParse(pc.Substring(prefixLen), out var c) && c > maxCounter)
                    maxCounter = c;
            }
            var nextCode = orgPrefix + (maxCounter + 1).ToString("D9");
            return Json(new { payCode = nextCode });
        }

        [RequirePermission("children.create")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateInvoices([FromBody] CreateInvoicesRequest request)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (request?.ClientIds == null || request.ClientIds.Count == 0)
                return BadRequest(new { success = false, message = "Выберите хотя бы одного получателя (группу или клиентов)." });
            var useManualService = request.ManualServicePriceSom.HasValue;
            if (!useManualService && (request.ServiceItems == null || request.ServiceItems.Count == 0))
                return BadRequest(new { success = false, message = "Выберите хотя бы одну услугу или укажите цену (режим «услуга по счёту»)." });
            if (useManualService && request.ManualServicePriceSom.Value < 0)
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
                ServiceItems = request.ServiceItems?.Select(x => new CreateInvoiceServiceItemInput { ServiceId = x.ServiceId, Qty = x.Qty }).ToList(),
                OrgServices = orgServices,
                PayCode = request.PayCode,
                Hassameaccount = request.Hassameaccount
            };

            try
            {
                var createdIds = await _operationsByInvoices.CreateInvoicesAsync(input);
                return Json(new { success = true, message = "Счета созданы.", createdIds });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return new JsonResult(new { success = false, message = "Ошибка БД: " + msg }) { StatusCode = 500 };
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return new JsonResult(new { success = false, message = "Ошибка: " + msg }) { StatusCode = 500 };
            }
        }

    }
}
