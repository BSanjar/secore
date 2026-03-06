using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
    [RequireAuth]
    public class CabinetController : Controller
    {
        private readonly AppDbContext _db;
        
        public CabinetController(AppDbContext db)
        {
            _db = db;
        }

        [RequirePermission("dashboard.view")]
        public async Task<IActionResult> Index()
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

            // Клиенты организации (активные для подсчёта клиентов)
            var clientIds = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId)
                .Select(c => c.Id)
                .ToListAsync();

            var clients = await _db.OrganizationClients
                .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
                .ToListAsync();

            // Счета организации (по клиентам)
            var invoiceIds = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .Select(i => i.Id)
                .ToListAsync();

            var invoices = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .ToListAsync();

            // Транзакции только по счетам организации (суммы в тыйынах)
            var transactions = await _db.Transactions
                .Where(t => t.TransactionStatus == "success" &&
                            t.Invoice != null &&
                            invoiceIds.Contains(t.Invoice))
                .ToListAsync();

            var now = DateTime.Now;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            // Приходы за сегодня (debit, в сомах)
            var todayIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                            t.TransactionDate.Value.Date == DateTime.Today &&
                            t.Summ.HasValue &&
                            t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

            // Приходы за месяц (debit, в сомах)
            var monthIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                           t.TransactionDate.Value.Month == currentMonth &&
                           t.TransactionDate.Value.Year == currentYear &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

            // Приходы за год (debit, в сомах)
            var yearIncome = transactions
                .Where(t => t.TransactionDate.HasValue &&
                           t.TransactionDate.Value.Year == currentYear &&
                           t.Summ.HasValue &&
                           t.TransactionType == "debit")
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

            // Должники: клиенты с отрицательной суммой балансов по счетам (actual)
            var balanceByClient = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client) && i.InvoiceStatus == "actual")
                .GroupBy(i => i.Client!)
                .Select(g => new { ClientId = g.Key, TotalBalance = g.Sum(i => i.Balance ?? 0m) })
                .ToDictionaryAsync(x => x.ClientId, x => x.TotalBalance);

            var debtorsCount = clientIds.Count(cid => balanceByClient.GetValueOrDefault(cid, 0m) < 0);
            var debtorsAmount = balanceByClient
                .Where(kv => kv.Value < 0)
                .Sum(kv => Math.Abs(kv.Value)) / 100m;

            var totalClients = clients.Count;

            var activeInvoices = invoices.Count(i => i.InvoiceStatus == "actual");

            ViewBag.TodayIncome = todayIncome;
            ViewBag.MonthIncome = monthIncome;
            ViewBag.YearIncome = yearIncome;
            ViewBag.DebtorsCount = debtorsCount;
            ViewBag.DebtorsAmount = debtorsAmount;
            ViewBag.TotalClients = totalClients;
            ViewBag.ActiveInvoices = activeInvoices;

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
                if (string.IsNullOrEmpty(organizationId))
                {
                    return Unauthorized();
                }

                using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    // Генерируем ID для OrganizationClient (последний ID + 1)
                    // Получаем все ID и находим максимальный числовой ID
                    var allClientIds = await _db.OrganizationClients
                        .Select(c => c.Id)
                        .ToListAsync();

                    int newClientId = 1;
                    foreach (var idStr in allClientIds)
                    {
                        if (int.TryParse(idStr, out int id) && id >= newClientId)
                        {
                            newClientId = id + 1;
                        }
                    }

                    // Создаем OrganizationClient
                    var organizationClient = new OrganizationClient
                    {
                        Id = newClientId.ToString(),
                        Organization = organizationId,
                        OrgClientGroupId = string.IsNullOrWhiteSpace(request.OrgClientGroupId) ? null : request.OrgClientGroupId,
                        ClientName = request.ClientName,
                        ClientType = "fiz",
                        ClientInn = request.ClientInn,
                        ClientPhone = request.ClientPhone,
                        ClientAddress = request.ClientAdres,
                        ClientEmail = request.ClientEmail,
                        ClientWa = request.ClientWa,
                        ClientTg = request.ClientTg,
                        ClientBalance = 0,
                        ClientStatus = 1,
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now,
                        UserCreater = userId
                    };

                    _db.OrganizationClients.Add(organizationClient);

                    // Дополнительные поля (OrganizationClientsAdditionalField)
                    if (request.AdditionalFields != null && request.AdditionalFields.Count > 0)
                    {
                        var orgFieldIds = await _db.OrganizationFields
                            .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
                            .Select(f => f.Id)
                            .ToListAsync();

                        foreach (var kv in request.AdditionalFields)
                        {
                            if (string.IsNullOrWhiteSpace(kv.Value) || !orgFieldIds.Contains(kv.Key)) continue;
                            _db.OrganizationClientsAdditionalFields.Add(new OrganizationClientsAdditionalField
                            {
                                Id = Guid.NewGuid().ToString(),
                                OrganizationClient = newClientId.ToString(),
                                Field = kv.Key,
                                Value = kv.Value.Trim()
                            });
                        }
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Ребенок успешно добавлен", clientId = newClientId.ToString() });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка при добавлении ребенка: {ex.Message}" });
            }
        }

        public class CreateChildRequest
        {
            public string? ClientName { get; set; }
            public string? ClientInn { get; set; }
            public string? OrgClientGroupId { get; set; }
            public string? ClientPhone { get; set; }
            public string? ClientAdres { get; set; }
            public string? ClientEmail { get; set; }
            public string? ClientWa { get; set; }
            public string? ClientTg { get; set; }
            /// <summary>Ключ — Id поля (OrganizationField), значение — введённое значение</summary>
            public Dictionary<string, string>? AdditionalFields { get; set; }
        }

        [RequirePermission("children.view")]
        public async Task<IActionResult> Children(string search = "", string statusFilter = "active", bool debtorsOnly = false)
        {
           
            
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }
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
                    (c.ClientAddress != null && c.ClientAddress.ToLower().Contains(searchLower)) ||
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

            // Сумма балансов по всем счетам для каждого клиента (нужна до фильтра должников)
            var clientIdsForBalance = childrenData.Select(c => c.Id).ToList();
            var totalBalanceByClient = await _db.Invoices
                .Where(i => i.Client != null && clientIdsForBalance.Contains(i.Client) && i.InvoiceStatus == "actual")
                .GroupBy(i => i.Client!)
                .Select(g => new { ClientId = g.Key, TotalBalance = g.Sum(i => i.Balance ?? 0m) })
                .ToDictionaryAsync(x => x.ClientId, x => x.TotalBalance);

            // Фильтр должников (активные с отрицательной суммой балансов по счетам)
            if (debtorsOnly)
            {
                childrenData = childrenData.Where(c =>
                    c.ClientStatus == 1 &&
                    totalBalanceByClient.GetValueOrDefault(c.Id, 0m) < 0).ToList();
            }

            // Получаем дополнительные поля для организации
            var organizationFields = await _db.OrganizationFields
                .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
                .ToListAsync();

            // Группы клиентов организации (для модального окна добавления и фильтра)
            var orgClientGroups = await _db.OrgClientGroups
                .Where(g => g.OrganizationId == organizationId && g.IsDeleted == 0)
                .OrderBy(g => g.Name)
                .ToListAsync();
            ViewBag.OrgClientGroups = orgClientGroups;
            ViewBag.ClientTotalBalanceByClientId = totalBalanceByClient;

            // Создаем список данных для View
            var childrenViewData = childrenData
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            ViewBag.Search = search;
            ViewBag.ClientTotalBalanceByClientId = totalBalanceByClient;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DebtorsOnly = debtorsOnly;
            ViewBag.OrganizationFields = organizationFields;
            ViewBag.ChildrenData = childrenViewData;

            return View();
        }

        [RequirePermission("children.view")]
        public async Task<IActionResult> GetChildInfo(string clientId)
        {
            var organizationId = HttpContext.Session.GetString("OrganizationId");
            if (string.IsNullOrEmpty(organizationId))
            {
                return Unauthorized();
            }

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
                    address = client.ClientAddress,
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
        public async Task<IActionResult> Payments(string dateFrom = "", string dateTo = "", List<string>? agentIds = null)
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
                .Where(t => t.Invoice != null && invoices.Contains(t.Invoice));

            if (DateTime.TryParse(dateFrom, out var fromDate))
                query = query.Where(t => t.TransactionDate >= fromDate.Date);
            if (DateTime.TryParse(dateTo, out var toDate))
                query = query.Where(t => t.TransactionDate != null && t.TransactionDate.Value.Date <= toDate.Date.AddDays(1));

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

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SelectedAgentIds = selectedAgentIds;
            ViewBag.AgentsList = agentsList;
            return View(transactions);
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
            return PartialView("_CreateInvoicePartial");
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

            var orgPrefix = organizationId;
            var prefixLen = orgPrefix.Length;
            var existingPayCodes = await _db.Invoices
                .Where(i => i.Client != null && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId && i.PayCode != null)
                .Select(i => i.PayCode)
                .ToListAsync();
            long maxCounter = 0;
            foreach (var pc in existingPayCodes)
            {
                if (pc != null && pc.Length == prefixLen + 9 && pc.StartsWith(orgPrefix) && long.TryParse(pc.Substring(prefixLen), out var c) && c > maxCounter)
                    maxCounter = c;
            }

            var useExistingPayCode = !string.IsNullOrWhiteSpace(request.PayCode);
            var createdIds = new List<string>();

            try
            {
                foreach (var client in clients)
                {
                    var payCode = useExistingPayCode ? request.PayCode!.Trim() : (orgPrefix + (++maxCounter).ToString("D9"));
                    var invoiceId = Guid.NewGuid().ToString();
                    // Храним даты как «без часового пояса» — то же значение, что ввёл пользователь (локальное)
                    var dateStart = request.DateStartInvoice ?? DateTime.Today;
                    if (dateStart.Kind != DateTimeKind.Unspecified)
                        dateStart = DateTime.SpecifyKind(dateStart.Kind == DateTimeKind.Utc ? dateStart.ToLocalTime() : dateStart, DateTimeKind.Unspecified);

                    var autoProlongation = request.AutoProlongation;
                    DateTime? dateEnd = null;
                    if (!autoProlongation && request.DateEndInvoice.HasValue)
                    {
                        dateEnd = request.DateEndInvoice.Value;
                        if (dateEnd.Value.Kind != DateTimeKind.Unspecified)
                            dateEnd = DateTime.SpecifyKind(dateEnd.Value.Kind == DateTimeKind.Utc ? dateEnd.Value.ToLocalTime() : dateEnd.Value, DateTimeKind.Unspecified);
                    }

                    decimal totalTyiyn;
                    if (useManualService)
                    {
                        totalTyiyn = (decimal)(request.ManualServicePriceSom!.Value * 100m);
                        _db.InvoiceServices.Add(new InvoiceService
                        {
                            Id = Guid.NewGuid().ToString(),
                            Invoice = invoiceId,
                            Service = null,
                            ServiceSumm = totalTyiyn
                        });
                    }
                    else
                    {
                        totalTyiyn = 0;
                        foreach (var item in request.ServiceItems!)
                        {
                            if (item.ServiceId == null || orgServices == null || !orgServices.TryGetValue(item.ServiceId, out var orgService))
                                continue;
                            var qty = Math.Max(1, item.Qty ?? 1);
                            var serviceSummTyiyn = orgService.ServiceSumm ?? 0;
                            totalTyiyn += serviceSummTyiyn * qty;
                            _db.InvoiceServices.Add(new InvoiceService
                            {
                                Id = Guid.NewGuid().ToString(),
                                Invoice = invoiceId,
                                Service = orgService.Id,
                                ServiceSumm = serviceSummTyiyn * qty
                            });
                        }
                    }

                    var periodicity = request.Periodicity ?? "monthly";
                    var useCurrentDateTime = request.UseCurrentDateTime;
                    var ru = CultureInfo.GetCultureInfo("ru-RU");

                    static DateTime GetPeriodStart(DateTime d, string periodicity)
                    {
                        if (periodicity == "monthly")
                            return new DateTime(d.Year, d.Month, 1, 0, 0, 0, d.Kind);
                        if (periodicity == "weekly")
                        {
                            var diff = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                            var monday = d.Date.AddDays(-diff);
                            return new DateTime(monday.Year, monday.Month, monday.Day, 0, 0, 0, d.Kind);
                        }
                        if (periodicity == "yearly")
                            return new DateTime(d.Year, 1, 1, 0, 0, 0, d.Kind);
                        return d;
                    }

                    static DateTime GetPeriodEnd(DateTime d, string periodicity)
                    {
                        if (periodicity == "monthly")
                            return new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month), 23, 59, 59, d.Kind);
                        if (periodicity == "weekly")
                        {
                            var start = GetPeriodStart(d, "weekly");
                            var endDate = start.AddDays(6).Date;
                            return new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, d.Kind);
                        }
                        if (periodicity == "yearly")
                            return new DateTime(d.Year, 12, 31, 23, 59, 59, d.Kind);
                        return d;
                    }

                    var invoice = new Invoice
                    {
                        Id = invoiceId,
                        DateCreated = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                        UserCreater = userId,
                        InvoiceStatus = "actual",
                        Periodicity = request.Periodicity ?? "monthly",
                        DateStartInvoice = dateStart,
                        DateEndInvoice = dateEnd,
                        Balance = 0,
                        FixedSumm = totalTyiyn,
                        PayCode = payCode,
                        Client = client.Id,
                        AutoProlongation = autoProlongation,
                        Hassameaccount = useExistingPayCode,
                        NameInvoice = !string.IsNullOrWhiteSpace(request.NameInvoice) ? request.NameInvoice : "Счет " + (client.ClientName ?? client.Id)
                    };
                    _db.Invoices.Add(invoice);

                    if (autoProlongation)
                    {
                        DateTime dateFrom;
                        DateTime dateTo;
                        string periodValue;
                        if (useCurrentDateTime)
                        {
                            if (periodicity == "weekly")
                            {
                                dateFrom = dateStart;
                                var endDay = dateStart.AddDays(6).Date;
                                dateTo = new DateTime(endDay.Year, endDay.Month, endDay.Day, 23, 59, 59, DateTimeKind.Unspecified);
                                periodValue = dateFrom.ToString("dd.MM.yyyy", ru) + " - " + dateTo.ToString("dd.MM.yyyy", ru);
                            }
                            else if (periodicity == "yearly")
                            {
                                dateFrom = dateStart;
                                dateTo = new DateTime(dateStart.Year, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
                                periodValue = dateStart.Year.ToString();
                            }
                            else
                            {
                                dateFrom = dateStart;
                                dateTo = dateStart.AddMonths(1).AddSeconds(-1);
                                periodValue = ru.DateTimeFormat.GetMonthName(dateStart.Month) + " " + dateStart.Year;
                            }
                        }
                        else
                        {
                            dateFrom = GetPeriodStart(dateStart, periodicity);
                            dateTo = GetPeriodEnd(dateStart, periodicity);
                            periodValue = periodicity == "weekly"
                                ? dateFrom.ToString("dd.MM.yyyy", ru) + " - " + dateTo.ToString("dd.MM.yyyy", ru)
                                : periodicity == "yearly"
                                    ? dateFrom.Year.ToString()
                                    : ru.DateTimeFormat.GetMonthName(dateFrom.Month) + " " + dateFrom.Year;
                        }
                        _db.InvoicePayments.Add(new InvoicePayment
                        {
                            Id = Guid.NewGuid().ToString(),
                            Invoice = invoiceId,
                            DateFrom = dateFrom,
                            DateTo = dateTo,
                            PaymentStatus = "non_paid",
                            PeriodValue = periodValue,
                            PaymentSumm = totalTyiyn
                        });
                    }
                    else if (dateEnd.HasValue)
                    {
                        var from = dateStart.Date;
                        var to = dateEnd.Value.Date;
                        if (from > to) (from, to) = (to, from);

                        if (periodicity == "weekly")
                        {
                            var periodStart = useCurrentDateTime
                                ? dateStart
                                : GetPeriodStart(dateStart, "weekly");
                            while (periodStart < dateEnd.Value)
                            {
                                var periodEnd = useCurrentDateTime
                                    ? new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 23, 59, 59, DateTimeKind.Unspecified).AddDays(6)
                                    : GetPeriodEnd(periodStart, "weekly");
                                var dateTo = periodEnd > dateEnd.Value ? dateEnd.Value : periodEnd;
                                var periodValue = periodStart.ToString("dd.MM.yyyy", ru) + " - " + dateTo.ToString("dd.MM.yyyy", ru);
                                _db.InvoicePayments.Add(new InvoicePayment
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Invoice = invoiceId,
                                    DateFrom = periodStart,
                                    DateTo = dateTo,
                                    PaymentStatus = "non_paid",
                                    PeriodValue = periodValue,
                                    PaymentSumm = totalTyiyn
                                });
                                periodStart = periodStart.AddDays(7);
                                if (!useCurrentDateTime)
                                    periodStart = new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 0, 0, 0, DateTimeKind.Unspecified);
                            }
                        }
                        else if (periodicity == "yearly")
                        {
                            if (useCurrentDateTime)
                            {
                                var periodStart = dateStart;
                                while (periodStart < dateEnd.Value)
                                {
                                    var periodEnd = new DateTime(periodStart.Year, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
                                    var dateTo = periodEnd > dateEnd.Value ? dateEnd.Value : periodEnd;
                                    _db.InvoicePayments.Add(new InvoicePayment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Invoice = invoiceId,
                                        DateFrom = periodStart,
                                        DateTo = dateTo,
                                        PaymentStatus = "non_paid",
                                        PeriodValue = periodStart.Year.ToString(),
                                        PaymentSumm = totalTyiyn
                                    });
                                    periodStart = new DateTime(periodStart.Year + 1, periodStart.Month, periodStart.Day, periodStart.Hour, periodStart.Minute, periodStart.Second, DateTimeKind.Unspecified);
                                }
                            }
                            else
                            {
                                var startYear = from.Year;
                                var endYear = to.Year;
                                for (var y = startYear; y <= endYear; y++)
                                {
                                    var dateFrom = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
                                    var dateTo = new DateTime(y, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
                                    _db.InvoicePayments.Add(new InvoicePayment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Invoice = invoiceId,
                                        DateFrom = dateFrom,
                                        DateTo = dateTo,
                                        PaymentStatus = "non_paid",
                                        PeriodValue = y.ToString(),
                                        PaymentSumm = totalTyiyn
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (useCurrentDateTime)
                            {
                                var periodStart = dateStart;
                                while (periodStart < dateEnd.Value)
                                {
                                    var periodEnd = periodStart.AddMonths(1).AddSeconds(-1);
                                    var dateTo = periodEnd > dateEnd.Value ? dateEnd.Value : periodEnd;
                                    var periodValue = ru.DateTimeFormat.GetMonthName(periodStart.Month) + " " + periodStart.Year;
                                    _db.InvoicePayments.Add(new InvoicePayment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Invoice = invoiceId,
                                        DateFrom = periodStart,
                                        DateTo = dateTo,
                                        PaymentStatus = "non_paid",
                                        PeriodValue = periodValue,
                                        PaymentSumm = totalTyiyn
                                    });
                                    periodStart = periodStart.AddMonths(1);
                                }
                            }
                            else
                            {
                                var endYear = to.Year;
                                var endMonth = to.Month;
                                for (var d = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Unspecified); d.Year < endYear || (d.Year == endYear && d.Month <= endMonth); d = d.AddMonths(1))
                                {
                                    var y = d.Year;
                                    var m = d.Month;
                                    var periodStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Unspecified);
                                    var periodEnd = new DateTime(y, m, DateTime.DaysInMonth(y, m), 23, 59, 59, DateTimeKind.Unspecified);
                                    var periodValue = ru.DateTimeFormat.GetMonthName(m) + " " + y;
                                    _db.InvoicePayments.Add(new InvoicePayment
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Invoice = invoiceId,
                                        DateFrom = periodStart,
                                        DateTo = periodEnd,
                                        PaymentStatus = "non_paid",
                                        PeriodValue = periodValue,
                                        PaymentSumm = totalTyiyn
                                    });
                                }
                            }
                        }
                    }

                    createdIds.Add(invoiceId);
                }

                await _db.SaveChangesAsync();
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

        public class CreateInvoicesRequest
        {
            public string? NameInvoice { get; set; }
            public DateTime? DateStartInvoice { get; set; }
            public DateTime? DateEndInvoice { get; set; }
            public string? Periodicity { get; set; }
            public bool AutoProlongation { get; set; }
            public bool UseCurrentDateTime { get; set; } = true;
            public List<string> ClientIds { get; set; } = new();
            public List<CreateInvoiceServiceItem> ServiceItems { get; set; } = new();
            /// <summary>Когда AllowedHassameaccount: выбранный лицевой счёт (все счета создаются с ним, Hassameaccount=true).</summary>
            public string? PayCode { get; set; }
            /// <summary>Когда DisableInvoiceServiceSelection: цена в сомах для одной позиции «название счёта».</summary>
            public decimal? ManualServicePriceSom { get; set; }
        }

        public class CreateInvoiceServiceItem
        {
            public string? ServiceId { get; set; }
            public int? Qty { get; set; }
        }
    }
}
