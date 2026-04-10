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

    public ClientsController(
        AppDbContext db,
        OperationsByInvoices operationsByInvoices,
        ClientService clientService,
        ClientPhotoService clientPhotoService,
        ICurrentTenantService currentTenantService)
    {
        _db = db;
        _operationsByInvoices = operationsByInvoices;
        _clientService = clientService;
        _clientPhotoService = clientPhotoService;
        _currentTenantService = currentTenantService;
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

        return View();
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

            return Json(new { success = true, message = "Клиент успешно добавлен", clientId });
        }
        catch (Exception ex)
        {
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

            return Json(new { success = true, message = "Данные клиента обновлены" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Ошибка при обновлении клиента: {ex.Message}" });
        }
    }

    [RequirePermission("invoices.view")]
    [HttpGet]
    public async Task<IActionResult> CreateInvoicePartial()
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

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
        ViewBag.InvoicePayCodeMode = !string.IsNullOrEmpty(settings?.InvoicePayCodeMode)
            ? settings.InvoicePayCodeMode
            : (settings?.AllowedHassameaccount == true ? "both" : "new_only");

        return PartialView("_CreateInvoicePartial");
    }

    [RequirePermission("invoices.view")]
    [HttpGet]
    public async Task<IActionResult> GetEditInvoicePartial([FromQuery] string invoiceId)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

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

        return PartialView("_EditInvoicePartial", invoice);
    }

    [RequirePermission("invoices.view")]
    [HttpGet]
    public async Task<IActionResult> GetInvoicePayCodeOptions([FromQuery] string clientIds)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        var ids = (clientIds ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Json(new { payCodeOptions = Array.Empty<object>() });

        var list = await _db.Invoices
            .Where(i => i.ClientNavigation != null &&
                        i.ClientNavigation.Organization == organizationId &&
                        i.Client != null &&
                        ids.Contains(i.Client) &&
                        i.Hassameaccount &&
                        i.PayCode != null &&
                        i.PayCode.Length > 0)
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
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

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
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

        var prefixLen = organizationId.Length;
        var clientIds = await _db.OrganizationClients
            .Where(c => c.Organization == organizationId)
            .Select(c => c.Id)
            .ToListAsync();

        var payCodes = await _db.Invoices
            .Where(i => i.Client != null &&
                        clientIds.Contains(i.Client) &&
                        i.PayCode != null &&
                        i.PayCode.Length == prefixLen + 9 &&
                        i.PayCode.StartsWith(organizationId))
            .Select(i => i.PayCode)
            .ToListAsync();

        long maxCounter = 0;
        foreach (var payCode in payCodes)
        {
            if (payCode != null &&
                payCode.Length > prefixLen &&
                long.TryParse(payCode.Substring(prefixLen), out var counter) &&
                counter > maxCounter)
            {
                maxCounter = counter;
            }
        }

        return Json(new { payCode = organizationId + (maxCounter + 1).ToString("D9") });
    }

    /// <summary>
    /// Загрузка или удаление фото клиента.
    /// </summary>
    [HttpPost]
    [RequirePermission("children.create")]
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

    [RequirePermission("invoices.view")]
    [HttpGet]
    public async Task<IActionResult> GetInvoicesInfo(string clientId, string? invoiceId = null)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

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

    [RequirePermission("children.create")]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateInvoices([FromBody] CreateInvoicesRequest request)
    {
        var organizationId = GetOrganizationIdOrNull();
        var userId = GetUserIdOrNull();
        if (organizationId == null || userId == null)
            return Unauthorized();

        var featureGuard = EnsureClientsFeature();
        if (featureGuard != null)
            return featureGuard;

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
            Hassameaccount = request.Hassameaccount
        };

        try
        {
            var createdIds = await _operationsByInvoices.CreateInvoicesAsync(input);
            return Json(new { success = true, message = "Счета созданы.", createdIds });
        }
        catch (DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return new JsonResult(new { success = false, message = "Ошибка БД: " + message }) { StatusCode = 500 };
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return new JsonResult(new { success = false, message = "Ошибка: " + message }) { StatusCode = 500 };
        }
    }

    private IActionResult? EnsureClientsFeature()
    {
        var tenant = _currentTenantService.GetCurrent();
        return tenant.Profile.HasFeature(CabinetFeatures.Clients) ? null : NotFound();
    }

    private string? GetOrganizationIdOrNull() => HttpContext.Session.GetString("OrganizationId");

    private string? GetUserIdOrNull() => HttpContext.Session.GetString("UserId");

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
