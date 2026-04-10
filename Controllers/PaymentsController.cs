using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;

namespace WebApplication1.Controllers;

[RequireAuth]
public class PaymentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ExcelExportService _excelExportService;
    private readonly OperationsByInvoices _operationsByInvoices;
    private readonly ICurrentTenantService _currentTenantService;

    public PaymentsController(
        AppDbContext db,
        ExcelExportService excelExportService,
        OperationsByInvoices operationsByInvoices,
        ICurrentTenantService currentTenantService)
    {
        _db = db;
        _excelExportService = excelExportService;
        _operationsByInvoices = operationsByInvoices;
        _currentTenantService = currentTenantService;
    }

    [RequirePermission("transactions.view")]
    public async Task<IActionResult> GetClientTransactions(string clientId, List<string>? agentIds = null)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return Unauthorized();

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var restrictToCurrentUser = IsDoctorRole() && IsMedclinicProfile() && !string.IsNullOrWhiteSpace(currentUserId);

        var featureGuard = EnsurePaymentsFeature();
        if (featureGuard != null)
            return featureGuard;

        var client = await _db.OrganizationClients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId);

        if (client == null)
            return NotFound();

        var invoices = await _db.Invoices
            .Where(i => i.Client == clientId && (!restrictToCurrentUser || i.UserCreater == currentUserId))
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

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

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
    public async Task<IActionResult> Index(
        string dateFrom = "",
        string dateTo = "",
        string search = "",
        string clientId = "",
        string statusFilter = "",
        List<string>? agentIds = null,
        [FromQuery] SimplePaymentsFilterParams? simpleFilters = null)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
                return RedirectToAction("Login", "Account");

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var restrictToCurrentUser = IsDoctorRole() && IsMedclinicProfile() && !string.IsNullOrWhiteSpace(currentUserId);

        var featureGuard = EnsurePaymentsFeature();
        if (featureGuard != null)
            return featureGuard;

        var tenant = _currentTenantService.GetCurrent();
        ViewBag.CanCreateOneTimePayment = SupportsOneTimePayment();
        if (string.Equals(tenant.Profile.Key, "simple", StringComparison.OrdinalIgnoreCase))
            return await RenderSimpleIndexAsync(organizationId, simpleFilters ?? new SimplePaymentsFilterParams());

        var invoices = await _db.Invoices
            .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
            .Where(i => !restrictToCurrentUser || i.UserCreater == currentUserId)
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
                (t.InvoiceNavigation != null &&
                 t.InvoiceNavigation.ClientNavigation != null &&
                 t.InvoiceNavigation.ClientNavigation.ClientName != null &&
                 t.InvoiceNavigation.ClientNavigation.ClientName.ToLower().Contains(term)) ||
                (t.InvoiceNavigation != null &&
                 t.InvoiceNavigation.PayCode != null &&
                 t.InvoiceNavigation.PayCode.ToLower().Contains(term)) ||
                (t.AgentNavigation != null &&
                 t.AgentNavigation.Name != null &&
                 t.AgentNavigation.Name.ToLower().Contains(term)));
        }

        var selectedAgentIds = agentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
            .Select(id => id!.Trim())
            .ToList() ?? new List<string>();

        if (selectedAgentIds.Count > 0)
            query = query.Where(t => t.Agent != null && selectedAgentIds.Contains(t.Agent));

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

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

        var clientsForDropdown = clientsList
            .Select(c => new { Id = c.Id, ClientName = c.ClientName ?? c.Id })
            .ToList();

        var selectedClientName = string.Empty;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var selectedClient = clientsList.FirstOrDefault(c => c.Id == clientId);
            selectedClientName = selectedClient?.ClientName ?? clientId;
        }

        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;
        ViewBag.Search = search ?? string.Empty;
        ViewBag.ClientId = clientId ?? string.Empty;
        ViewBag.SelectedClientName = selectedClientName;
        ViewBag.StatusFilter = statusFilter ?? string.Empty;
        ViewBag.SelectedAgentIds = selectedAgentIds;
        ViewBag.AgentsList = agentsList;
        ViewBag.ClientsList = clientsList;
        ViewBag.Clients = clientsForDropdown;

        return View(transactions);
    }

    [HttpGet]
    [RequirePermission("payments.create")]
    public async Task<IActionResult> Create()
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
                return RedirectToAction("Login", "Account");

        if (IsDoctorRole())
            return Forbid();

        if (!SupportsOneTimePayment())
            return NotFound();

        ViewBag.Clients = await _db.OrganizationClients
            .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
            .OrderBy(c => c.ClientName)
            .Select(c => new { c.Id, c.ClientName })
            .ToListAsync();

        ViewData["Title"] = IsMedclinicProfile() ? "Создать платёж" : "Создать одноразовый платёж";
        return View("~/Views/Payments/Create.cshtml", new CreatePaymentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("payments.create")]
    public async Task<IActionResult> Create(CreatePaymentViewModel model)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
            return RedirectToAction("Login", "Account");

        if (IsDoctorRole())
            return Forbid();

        if (!SupportsOneTimePayment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(model.ClientId))
            ModelState.AddModelError(nameof(model.ClientId), "Выберите клиента");
        if (model.AmountSom <= 0)
            ModelState.AddModelError(nameof(model.AmountSom), "Сумма должна быть больше 0");

        var client = string.IsNullOrWhiteSpace(model.ClientId)
            ? null
            : await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == model.ClientId && c.Organization == organizationId && c.ClientStatus == 1);

        if (model.ClientId != null && client == null)
            ModelState.AddModelError(nameof(model.ClientId), "Клиент не найден");

        if (!ModelState.IsValid)
        {
            ViewBag.Clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();
            ViewData["Title"] = IsMedclinicProfile() ? "Создать платёж" : "Создать одноразовый платёж";
            return View("~/Views/Payments/Create.cshtml", model);
        }

        try
        {
            var simpleUserId = AuthorizationHelper.GetUserId(HttpContext);
            if (string.IsNullOrWhiteSpace(simpleUserId))
            {
                return Unauthorized();
            }

            var simpleResult = await _operationsByInvoices.CreateOneTimeInvoiceAsync(new CreateOneTimeInvoiceInput
            {
                OrganizationId = organizationId,
                UserId = simpleUserId,
                ClientId = client!.Id,
                FixedSumm = model.AmountSom,
                InvoiceName = model.InvoiceName,
                PayCode = model.PayCode,
                DateStartInvoice = ParsersHelper.NowForTimestamp(),
                DateEndInvoice = ParsersHelper.NowForTimestamp(),
                PaymentDateFrom = ParsersHelper.NowForTimestamp(),
                PaymentDateTo = ParsersHelper.NowForTimestamp(),
                PaymentPeriodValue = ParsersHelper.NowForTimestamp().ToString("dd.MM.yyyy"),
                PaymentSumm = model.AmountSom
            });

            TempData["Success"] = $"Счёт создан. PayCode: {simpleResult.PayCode}";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Ошибка при создании платежа: " + ex.Message);
            ViewBag.Clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .OrderBy(c => c.ClientName)
                .Select(c => new { c.Id, c.ClientName })
                .ToListAsync();
            ViewData["Title"] = IsMedclinicProfile() ? "Создать платёж" : "Создать одноразовый платёж";
            return View("~/Views/Payments/Create.cshtml", model);
        }
    }

    [RequirePermission("transactions.view")]
    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        string dateFrom = "",
        string dateTo = "",
        string search = "",
        string clientId = "",
        string statusFilter = "",
        List<string>? agentIds = null)
    {
        var organizationId = GetOrganizationIdOrNull();
        if (organizationId == null)
                return RedirectToAction("Login", "Account");

        var currentUserId = AuthorizationHelper.GetUserId(HttpContext);
        var restrictToCurrentUser = IsDoctorRole() && IsMedclinicProfile() && !string.IsNullOrWhiteSpace(currentUserId);

        var featureGuard = EnsurePaymentsFeature();
        if (featureGuard != null)
            return featureGuard;

        var invoices = await _db.Invoices
            .Where(i => i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId)
            .Where(i => !restrictToCurrentUser || i.UserCreater == currentUserId)
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
                (t.InvoiceNavigation != null &&
                 t.InvoiceNavigation.ClientNavigation != null &&
                 t.InvoiceNavigation.ClientNavigation.ClientName != null &&
                 t.InvoiceNavigation.ClientNavigation.ClientName.ToLower().Contains(term)) ||
                (t.InvoiceNavigation != null &&
                 t.InvoiceNavigation.PayCode != null &&
                 t.InvoiceNavigation.PayCode.ToLower().Contains(term)) ||
                (t.AgentNavigation != null &&
                 t.AgentNavigation.Name != null &&
                 t.AgentNavigation.Name.ToLower().Contains(term)));
        }

        var selectedAgentIds = agentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
            .Select(id => id!.Trim())
            .ToList() ?? new List<string>();

        if (selectedAgentIds.Count > 0)
            query = query.Where(t => t.Agent != null && selectedAgentIds.Contains(t.Agent));

        var list = await query
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

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

    private IActionResult? EnsurePaymentsFeature()
    {
        var tenant = _currentTenantService.GetCurrent();
        return tenant.Profile.HasFeature(CabinetFeatures.Payments) ? null : NotFound();
    }

    private bool IsSimpleProfile()
    {
        var tenant = _currentTenantService.GetCurrent();
        return string.Equals(tenant.Profile.Key, "simple", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMedclinicProfile()
    {
        var tenant = _currentTenantService.GetCurrent();
        return string.Equals(tenant.Profile.Key, "medclinic", StringComparison.OrdinalIgnoreCase);
    }

    private bool SupportsOneTimePayment() => IsSimpleProfile() || IsMedclinicProfile();

    private bool IsDoctorRole() => AuthorizationHelper.IsDoctorRole(HttpContext);

    private async Task<IActionResult> RenderSimpleIndexAsync(string organizationId, SimplePaymentsFilterParams filters)
    {
        filters ??= new SimplePaymentsFilterParams();
        if (filters.PageNumber <= 0)
            filters.PageNumber = 1;
        if (filters.PageSize <= 0)
            filters.PageSize = 10;
        filters.SortDirection = string.IsNullOrWhiteSpace(filters.SortDirection)
            ? "DESC"
            : filters.SortDirection.ToUpperInvariant();

        var query = _db.Transactions
            .AsNoTracking()
            .Include(t => t.InvoiceNavigation!)
                .ThenInclude(i => i.ClientNavigation)
            .Where(t =>
                t.TransactionType == "debit" &&
                t.InvoiceNavigation != null &&
                t.InvoiceNavigation.ClientNavigation != null &&
                t.InvoiceNavigation.ClientNavigation.Organization == organizationId);

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            var normalizedStatus = filters.Status.Trim();
            query = query.Where(t => EF.Functions.ILike(t.TransactionStatus ?? string.Empty, normalizedStatus));
        }

        if (filters.DateFrom.HasValue)
        {
            var from = filters.DateFrom.Value.Date;
            query = query.Where(t => t.TransactionDate >= from);
        }

        if (filters.DateTo.HasValue)
        {
            var to = filters.DateTo.Value.Date.AddDays(1);
            query = query.Where(t => t.TransactionDate < to);
        }

        if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
            query = ApplySimpleSearch(query, filters);

        query = ApplySimpleSorting(query, filters);

        var totalItems = await query.CountAsync();
        var skip = (filters.PageNumber - 1) * filters.PageSize;

        var items = await query
            .Skip(skip)
            .Take(filters.PageSize)
            .Select(t => new PaymentListItemVm
            {
                TransactionId = t.Id,
                Date = t.TransactionDate,
                ClientName = t.InvoiceNavigation!.ClientNavigation!.ClientName,
                InvoiceName = t.InvoiceNavigation.NameInvoice,
                AmountSom = (t.Summ ?? 0) / 100m,
                Status = t.TransactionStatus
            })
            .ToListAsync();

        var result = new PagedResult<PaymentListItemVm>
        {
            Items = items,
            PageNumber = filters.PageNumber,
            PageSize = filters.PageSize,
            TotalItems = totalItems
        };

        var tableViewModel = new GenericTableViewModel<object>
        {
            Items = result.Items.Cast<object>().ToList(),
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages,
            Filters = filters,
            TableTitle = "История платежей",
            PartialHeaderViewName = "~/Views/Payments/Partials/_SimplePaymentsTableHeader.cshtml",
            PartialRowViewName = "~/Views/Payments/Partials/_SimplePaymentsTableRow.cshtml",
            LoadUrl = Url.Action(nameof(Index), "Payments") ?? string.Empty,
            ExportUrl = null,
            SearchableColumns = new Dictionary<string, string>
            {
                ["ClientName"] = "Клиент",
                ["InvoiceName"] = "Счёт",
                ["Status"] = "Статус"
            },
            FilterPartialViewName = "~/Views/Payments/Partials/_SimplePaymentsFilters.cshtml",
            EmptyStateTitle = "Нет платежей",
            EmptyStateDescription = "Создайте первый платёж, чтобы увидеть историю."
        };

        var pageViewModel = new SimplePaymentsIndexViewModel
        {
            Table = tableViewModel,
            CreatePaymentUrl = Url.Action(nameof(Create), "Payments") ?? string.Empty,
            FlashMessage = TempData["Success"] as string
        };

        return View("~/Views/Payments/SimpleIndex.cshtml", pageViewModel);
    }

    private static IQueryable<Transaction> ApplySimpleSearch(IQueryable<Transaction> query, SimplePaymentsFilterParams filters)
    {
        var term = filters.SearchTerm!.Trim();
        var likeTerm = $"%{term}%";

        return filters.SearchColumn?.ToLower() switch
        {
            "clientname" => query.Where(t =>
                t.InvoiceNavigation != null &&
                t.InvoiceNavigation.ClientNavigation != null &&
                EF.Functions.ILike(t.InvoiceNavigation.ClientNavigation.ClientName ?? string.Empty, likeTerm)),
            "invoicename" => query.Where(t =>
                t.InvoiceNavigation != null &&
                EF.Functions.ILike(t.InvoiceNavigation.NameInvoice ?? string.Empty, likeTerm)),
            "status" => query.Where(t =>
                EF.Functions.ILike(t.TransactionStatus ?? string.Empty, likeTerm)),
            _ => query.Where(t =>
                (t.InvoiceNavigation != null &&
                 t.InvoiceNavigation.ClientNavigation != null &&
                 EF.Functions.ILike(t.InvoiceNavigation.ClientNavigation.ClientName ?? string.Empty, likeTerm))
                || (t.InvoiceNavigation != null &&
                    EF.Functions.ILike(t.InvoiceNavigation.NameInvoice ?? string.Empty, likeTerm))
                || EF.Functions.ILike(t.TransactionStatus ?? string.Empty, likeTerm))
        };
    }

    private static IQueryable<Transaction> ApplySimpleSorting(IQueryable<Transaction> query, SimplePaymentsFilterParams filters)
    {
        var sortColumn = filters.SortColumn?.ToLower();
        var desc = string.Equals(filters.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase);

        return sortColumn switch
        {
            "clientname" => desc
                ? query.OrderByDescending(t => t.InvoiceNavigation!.ClientNavigation!.ClientName)
                : query.OrderBy(t => t.InvoiceNavigation!.ClientNavigation!.ClientName),
            "invoicename" => desc
                ? query.OrderByDescending(t => t.InvoiceNavigation!.NameInvoice)
                : query.OrderBy(t => t.InvoiceNavigation!.NameInvoice),
            "amountsom" => desc
                ? query.OrderByDescending(t => t.Summ ?? 0)
                : query.OrderBy(t => t.Summ ?? 0),
            "status" => desc
                ? query.OrderByDescending(t => t.TransactionStatus)
                : query.OrderBy(t => t.TransactionStatus),
            _ => desc
                ? query.OrderByDescending(t => t.TransactionDate)
                : query.OrderBy(t => t.TransactionDate)
        };
    }

    private string? GetOrganizationIdOrNull() => HttpContext.Session.GetString("OrganizationId");
}
