using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers;

[RequireAuth]
public class ClientsController : Controller
{
    private const string ClientsImportSessionPrefix = "__clients_import:";
    private const string OpenClientsImportModalKey = "OpenClientsImportModal";
    private const string ClientsImportIdKey = "ClientsImportId";

    private readonly AppDbContext _db;
    private readonly OperationsByInvoices _operationsByInvoices;
    private readonly ClientService _clientService;
    private readonly ClientPhotoService _clientPhotoService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        AppDbContext db,
        OperationsByInvoices operationsByInvoices,
        ClientService clientService,
        ClientPhotoService clientPhotoService,
        ICurrentTenantService currentTenantService,
        ILogger<ClientsController> logger)
    {
        _db = db;
        _operationsByInvoices = operationsByInvoices;
        _clientService = clientService;
        _clientPhotoService = clientPhotoService;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    [RequirePermission("nav.children")]
    [HttpGet]
    public async Task<IActionResult> Index(string search = "", string statusFilter = "active", bool debtorsOnly = false)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        var result = await _clientService.GetClientsForCabinetAsync(organizationId, search, statusFilter, debtorsOnly);

        var openImportModal = TempData[OpenClientsImportModalKey] != null;
        if (TempData.ContainsKey(ClientsImportIdKey))
        {
            var importId = TempData[ClientsImportIdKey]?.ToString();
            if (!string.IsNullOrEmpty(importId))
            {
                var json = HttpContext.Session.GetString(ClientsImportSessionPrefix + importId);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var previewRows = JsonSerializer.Deserialize<List<ChildrenImportRowDto>>(json) ?? new List<ChildrenImportRowDto>();
                        ViewBag.ClientsImportPreviewId = importId;
                        ViewBag.ClientsImportPreviewRows = previewRows;
                        openImportModal = true;
                    }
                    catch
                    {
                    }
                }
            }
        }

        ViewBag.OpenClientsImportModal = openImportModal;
        ViewBag.Search = search;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.DebtorsOnly = debtorsOnly;
        ViewBag.ClientsData = result.ChildrenData;
        ViewBag.ClientTotalBalanceByClientId = result.ClientTotalBalanceByClientId;
        ViewBag.FirstInvoiceIdByClientId = result.FirstInvoiceIdByClientId;
        ViewBag.OrganizationFields = result.OrganizationFields;
        ViewBag.OrgClientGroups = result.OrgClientGroups;
        ViewBag.InvoicePayCodeMode = result.InvoicePayCodeMode;
        ViewBag.AllowedHassameaccount = result.AllowedHassameaccount;

        var clientIds = result.ChildrenData
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var activeInvoiceCountByClientId = new Dictionary<string, int>();
        var debtInvoiceCountByClientId = new Dictionary<string, int>();
        var nearestDueByClientId = new Dictionary<string, DateTime?>();

        if (clientIds.Count > 0)
        {
            var invoices = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .Include(i => i.InvoicePayments)
                .ToListAsync();

            activeInvoiceCountByClientId = invoices
                .Where(i => !string.IsNullOrWhiteSpace(i.Client))
                .GroupBy(i => i.Client!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(i => string.Equals(i.InvoiceStatus, "actual", StringComparison.OrdinalIgnoreCase)));

            debtInvoiceCountByClientId = invoices
                .Where(i => !string.IsNullOrWhiteSpace(i.Client))
                .GroupBy(i => i.Client!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(i => (i.Balance ?? 0m) < 0m));

            nearestDueByClientId = invoices
                .Where(i => !string.IsNullOrWhiteSpace(i.Client))
                .GroupBy(i => i.Client!)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(i => i.InvoicePayments ?? Array.Empty<InvoicePayment>())
                        .Where(p => string.Equals(p.PaymentStatus, "non_paid", StringComparison.OrdinalIgnoreCase))
                        .Select(p => p.DateFrom)
                        .Where(d => d.HasValue)
                        .OrderBy(d => d)
                        .FirstOrDefault());
        }

        ViewBag.ActiveInvoiceCountByClientId = activeInvoiceCountByClientId;
        ViewBag.DebtInvoiceCountByClientId = debtInvoiceCountByClientId;
        ViewBag.NearestDueByClientId = nearestDueByClientId;

        var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
        if (string.Equals(organizationType, "standart", StringComparison.OrdinalIgnoreCase))
            return View("~/Views/Clients/StandartIndex.cshtml");

        return View();
    }

    [RequirePermission("nav.children")]
    [HttpGet]
    public async Task<IActionResult> GetClientInvoicesDashboard(string clientId)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { success = false, message = "Не указан clientId." });

        var client = await _db.OrganizationClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

        if (client == null)
            return NotFound(new { success = false, message = "Клиент не найден." });

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Client == clientId)
            .Include(i => i.InvoiceServices)
                .ThenInclude(s => s.ServiceNavigation)
            .Include(i => i.InvoicePayments)
            .Include(i => i.Transactions)
                .ThenInclude(t => t.AgentNavigation)
            .Include(i => i.InvoiceQrs)
            .OrderByDescending(i => i.DateCreated)
            .ToListAsync();

        var totalBalanceSom = invoices.Sum(i => (i.Balance ?? 0m) / 100m);
        var activeInvoices = invoices.Count(i => string.Equals(i.InvoiceStatus, "actual", StringComparison.OrdinalIgnoreCase));
        var debtInvoices = invoices.Count(i => (i.Balance ?? 0m) < 0m);
        var nearestDue = invoices
            .SelectMany(i => i.InvoicePayments ?? Array.Empty<InvoicePayment>())
            .Where(p => string.Equals(p.PaymentStatus, "non_paid", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.DateFrom)
            .Where(d => d.HasValue)
            .OrderBy(d => d)
            .FirstOrDefault();

        var payload = new
        {
            success = true,
            client = new
            {
                id = client.Id,
                name = client.ClientName ?? "Клиент",
                phone = client.ClientPhone,
                totalBalanceSom,
                nearestDue,
                activeInvoices,
                debtInvoices
            },
            invoices = invoices.Select(i =>
            {
                var latestQr = (i.InvoiceQrs ?? Array.Empty<InvoiceQr>())
                    .OrderByDescending(q => q.CreatedAt)
                    .FirstOrDefault();

                return new
                {
                    id = i.Id,
                    nameInvoice = i.NameInvoice,
                    payCode = i.PayCode,
                    invoiceStatus = i.InvoiceStatus,
                    periodicity = i.Periodicity,
                    dateCreated = i.DateCreated,
                    dateStartInvoice = i.DateStartInvoice,
                    dateEndInvoice = i.DateEndInvoice,
                    nextStartInvoice = i.NextStartInvoice,
                    fixedSummSom = (i.FixedSumm ?? 0m) / 100m,
                    balanceSom = (i.Balance ?? 0m) / 100m,
                    hasSameAccount = i.Hassameaccount,
                    autoProlongation = i.AutoProlongation ?? false,
                    services = (i.InvoiceServices ?? Array.Empty<InvoiceService>())
                        .Select(s => new
                        {
                            serviceName = s.ServiceNavigation?.Name ?? i.NameInvoice ?? "Услуга",
                            serviceSummSom = (s.ServiceSumm ?? 0m) / 100m
                        })
                        .ToList(),
                    payments = (i.InvoicePayments ?? Array.Empty<InvoicePayment>())
                        .OrderBy(p => p.DateFrom)
                        .Select(p => new
                        {
                            dateFrom = p.DateFrom,
                            dateTo = p.DateTo,
                            periodValue = p.PeriodValue,
                            paymentSummSom = (p.PaymentSumm ?? 0m) / 100m,
                            paymentStatus = p.PaymentStatus
                        })
                        .ToList(),
                    transactions = (i.Transactions ?? Array.Empty<Transaction>())
                        .OrderByDescending(t => t.TransactionDate)
                        .Select(t => new
                        {
                            transactionDate = t.TransactionDate,
                            transactionStatus = t.TransactionStatus,
                            summSom = (t.Summ ?? 0m) / 100m,
                            transactionSummSom = (t.TransactionSumm ?? 0m) / 100m,
                            transactionType = t.TransactionType,
                            transactionSystem = t.TransactionSystem,
                            agent = t.AgentNavigation == null
                                ? null
                                : new
                                {
                                    name = t.AgentNavigation.Name
                                },
                            txnId = t.TxnId
                        })
                        .ToList(),
                    qr = latestQr == null
                        ? null
                        : new
                        {
                            status = latestQr.Status,
                            createdAt = latestQr.CreatedAt,
                            disabledAt = latestQr.DisabledAt,
                            qrLink = latestQr.QrLink,
                            qrCodeBase64 = latestQr.QrCodeBase64
                        }
                };
            }).ToList()
        };

        return Json(payload);
    }

    [RequirePermission("children.create")]
    [HttpGet]
    public IActionResult DownloadImportTemplate()
    {
        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Клиенты");
        ws.Cell(1, 1).Value = "ФИО";
        ws.Cell(1, 2).Value = "ИНН";
        ws.Cell(1, 3).Value = "Номер телефона";
        ws.Cell(1, 4).Value = "Адрес";
        ws.Cell(1, 5).Value = "Эл. почта";
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"clients_import_template_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [RequirePermission("children.create")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewImport(IFormFile file)
    {
        var organizationId = GetOrganizationIdOrNull();
        var userId = GetUserIdOrNull();
        if (organizationId == null || userId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Выберите файл Excel и повторите попытку.";
            TempData[OpenClientsImportModalKey] = true;
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                TempData["Error"] = "Не удалось прочитать лист Excel.";
                TempData[OpenClientsImportModalKey] = true;
                return RedirectToAction(nameof(Index));
            }

            var colMap = BuildImportColumnMap(ws);
            var rows = BuildImportRows(ws, colMap);
            if (!rows.Any())
            {
                TempData["Error"] = "В файле нет строк с данными.";
                TempData[OpenClientsImportModalKey] = true;
                return RedirectToAction(nameof(Index));
            }

            var importId = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(ClientsImportSessionPrefix + importId, JsonSerializer.Serialize(rows));
            TempData[ClientsImportIdKey] = importId;
            TempData[OpenClientsImportModalKey] = true;
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка при разборе файла: {ex.Message}";
            TempData[OpenClientsImportModalKey] = true;
        }

        return RedirectToAction(nameof(Index));
    }

    [RequirePermission("children.create")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport(string importId)
    {
        var organizationId = GetOrganizationIdOrNull();
        var userId = GetUserIdOrNull();
        if (organizationId == null || userId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        if (string.IsNullOrWhiteSpace(importId))
        {
            TempData["Error"] = "Не найден идентификатор импорта.";
            return RedirectToAction(nameof(Index));
        }

        var json = HttpContext.Session.GetString(ClientsImportSessionPrefix + importId);
        if (string.IsNullOrEmpty(json))
        {
            TempData["Error"] = "Данные импорта не найдены или устарели.";
            return RedirectToAction(nameof(Index));
        }

        List<ChildrenImportRowDto>? rows;
        try
        {
            rows = JsonSerializer.Deserialize<List<ChildrenImportRowDto>>(json) ?? new List<ChildrenImportRowDto>();
        }
        catch
        {
            TempData["Error"] = "Не удалось прочитать данные импорта.";
            return RedirectToAction(nameof(Index));
        }

        var imported = 0;
        var skipped = 0;

        foreach (var model in rows)
        {
            if (model == null || !model.IsValid || string.IsNullOrWhiteSpace(model.ClientName) || string.IsNullOrWhiteSpace(model.ClientInn))
            {
                skipped++;
                continue;
            }

            try
            {
                await _clientService.CreateClientAsync(new CreateClientInput
                {
                    ClientName = model.ClientName,
                    ClientInn = model.ClientInn.Trim(),
                    ClientPhone = string.IsNullOrWhiteSpace(model.ClientPhone) ? null : model.ClientPhone,
                    ClientAddress = string.IsNullOrWhiteSpace(model.ClientAddress) ? null : model.ClientAddress,
                    ClientEmail = string.IsNullOrWhiteSpace(model.ClientEmail) ? null : model.ClientEmail
                }, organizationId, userId);
                imported++;
            }
            catch
            {
                skipped++;
            }
        }

        HttpContext.Session.Remove(ClientsImportSessionPrefix + importId);
        TempData["Success"] = $"Импорт завершён. Добавлено: {imported}, пропущено: {skipped}.";
        return RedirectToAction(nameof(Index));
    }

    [RequirePermission("children.create")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateChildRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdOrNull();
            var userId = GetUserIdOrNull();
            if (organizationId == null || userId == null)
                return Unauthorized();

            var featureGuard = EnsureClientsFeature();
            if (featureGuard != null)
                return featureGuard;

            var clientId = await _clientService.CreateClientAsync(new CreateClientInput
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
            }, organizationId, userId);

            _logger.LogInformation(
                "Client created. ClientId={ClientId} Name={Name} Org={OrgId} User={UserId}",
                clientId, request.ClientName, organizationId, userId);

            return Json(new { success = true, message = "Клиент успешно добавлен", clientId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Client create failed. Name={Name}", request?.ClientName);
            return Json(new { success = false, message = $"Ошибка при добавлении клиента: {ex.Message}" });
        }
    }

    [RequirePermission("nav.children")]
    [HttpGet]
    public async Task<IActionResult> GetInfo(string clientId)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        if (string.IsNullOrWhiteSpace(clientId))
            return BadRequest(new { success = false, message = "Не указан clientId" });

        var clientInfo = await _clientService.GetClientInfoAsync(clientId, organizationId);
        if (clientInfo == null)
            return NotFound(new { success = false, message = "Клиент не найден" });

        return Json(new { success = true, client = clientInfo });
    }

    [RequirePermission("children.edit")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([FromBody] UpdateChildRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdOrNull();
            if (organizationId == null)
                return Unauthorized();

            var featureGuard = EnsureClientsFeature();
            if (featureGuard != null)
                return featureGuard;

            var updated = await _clientService.UpdateClientAsync(new UpdateClientInput
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
            }, organizationId);

            if (!updated)
                return NotFound(new { success = false, message = "Клиент не найден" });

            _logger.LogInformation(
                "Client updated. ClientId={ClientId} Name={Name} Status={Status} Org={OrgId}",
                request.ClientId, request.ClientName, request.ClientStatus, organizationId);

            return Json(new { success = true, message = "Данные клиента обновлены" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Client update failed. ClientId={ClientId}", request?.ClientId);
            return Json(new { success = false, message = $"Ошибка при обновлении клиента: {ex.Message}" });
        }
    }


    /// <summary>
    /// Загрузка или удаление фото клиента.
    /// </summary>
    [HttpPost]
    [RequirePermission("children.create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto([FromForm] string? clientId, IFormFile? photo, [FromForm] bool removePhoto = false)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        if (string.IsNullOrWhiteSpace(clientId))
            return Json(new { success = false, message = "Не указан клиент." });

        var client = await _db.OrganizationClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

        if (client == null)
            return Json(new { success = false, message = "Клиент не найден." });

        try
        {
            if (removePhoto)
            {
                _clientPhotoService.DeleteFileIfExists(client.ClientLogo);
                await _clientService.SetClientLogoAsync(clientId, organizationId, null);
                return Json(new { success = true, message = "Фото удалено.", logoUrl = (string?)null });
            }

            if (photo == null || photo.Length == 0)
                return Json(new { success = false, message = "Выберите файл изображения." });

            _clientPhotoService.DeleteFileIfExists(client.ClientLogo);
            var relativeUrl = await _clientPhotoService.SaveAndCompressAsync(photo, organizationId, clientId);
            await _clientService.SetClientLogoAsync(clientId, organizationId, relativeUrl);
            return Json(new { success = true, message = "Фото сохранено.", logoUrl = relativeUrl });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }


    private IActionResult? EnsureClientsFeature()
    {
        var tenant = _currentTenantService.GetCurrent();
        return tenant.Profile.HasFeature(CabinetFeatures.Clients) ? null : NotFound();
    }

    private string? GetOrganizationIdOrNull() => HttpContext.Session.GetString("OrganizationId");

    private string? GetUserIdOrNull() => HttpContext.Session.GetString("UserId");

    private static string NormalizeImportHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant().Replace("ё", "е");
    }

    private static Dictionary<string, int> BuildImportColumnMap(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstRow = ws.FirstRowUsed();
        if (firstRow == null)
            return map;

        var row = firstRow.RowNumber();
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 5;
        for (var col = 1; col <= lastCol; col++)
        {
            var header = NormalizeImportHeader(ws.Cell(row, col).GetString());
            if (string.IsNullOrEmpty(header))
                continue;

            if (!map.ContainsKey("fio") && (header == "фио" || header.Contains("фио")))
                map["fio"] = col;
            else if (!map.ContainsKey("inn") && header.Contains("инн"))
                map["inn"] = col;
            else if (!map.ContainsKey("phone") && (header.Contains("телефон") || header.Contains("номер")))
                map["phone"] = col;
            else if (!map.ContainsKey("address") && header.Contains("адрес"))
                map["address"] = col;
            else if (!map.ContainsKey("email") && (header.Contains("почт") || header.Contains("email") || header.Contains("e-mail") || header.Contains("эл")))
                map["email"] = col;
        }

        return map;
    }

    private static List<ChildrenImportRowDto> BuildImportRows(IXLWorksheet ws, Dictionary<string, int> map)
    {
        var rows = new List<ChildrenImportRowDto>();
        var headerRow = ws.FirstRowUsed()?.RowNumber() ?? 1;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var row = headerRow + 1; row <= lastRow; row++)
        {
            var fio = GetImportCellTrim(ws, row, ImportCol(map, "fio", 1));
            var inn = GetImportCellTrim(ws, row, ImportCol(map, "inn", 2));
            var phone = GetImportCellTrim(ws, row, ImportCol(map, "phone", 3));
            var address = GetImportCellTrim(ws, row, ImportCol(map, "address", 4));
            var email = GetImportCellTrim(ws, row, ImportCol(map, "email", 5));

            if (string.IsNullOrWhiteSpace(fio) &&
                string.IsNullOrWhiteSpace(inn) &&
                string.IsNullOrWhiteSpace(phone) &&
                string.IsNullOrWhiteSpace(address) &&
                string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var dto = new ChildrenImportRowDto
            {
                ClientName = string.IsNullOrWhiteSpace(fio) ? null : fio,
                ClientInn = string.IsNullOrWhiteSpace(inn) ? null : inn,
                ClientPhone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                ClientAddress = string.IsNullOrWhiteSpace(address) ? null : address,
                ClientEmail = string.IsNullOrWhiteSpace(email) ? null : email
            };

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(dto.ClientName))
                missing.Add("ФИО");
            if (string.IsNullOrWhiteSpace(dto.ClientInn))
                missing.Add("ИНН");

            dto.IsValid = missing.Count == 0;
            dto.Error = missing.Count == 0 ? null : "Обязательно: " + string.Join(", ", missing);
            rows.Add(dto);
        }

        return rows;
    }

    private static int ImportCol(Dictionary<string, int> map, string key, int fallback) =>
        map.TryGetValue(key, out var value) ? value : fallback;

    private static string GetImportCellTrim(IXLWorksheet ws, int rowNum, int col)
    {
        if (col < 1 || rowNum < 1)
            return string.Empty;

        try
        {
            return ws.Cell(rowNum, col).GetString()?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
