using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        private readonly OperationsByInvoices _operationsByInvoices;

        public CabinetController(AppDbContext db, OperationsByInvoices operationsByInvoices)
        {
            _db = db;
            _operationsByInvoices = operationsByInvoices;
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
                {
                    return Unauthorized();
                }

                var clientId = await CreateChildInternalAsync(request, organizationId, userId);
                return Json(new { success = true, message = "Ребенок успешно добавлен", clientId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Ошибка при добавлении ребенка: {ex.Message}" });
            }
        }

        private async Task<string> CreateChildInternalAsync(CreateChildRequest request, string organizationId, string userId)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var allClientIds = await _db.OrganizationClients
                    .Select(c => c.Id)
                    .ToListAsync();

                int newClientId = 1;
                foreach (var idStr in allClientIds)
                {
                    if (int.TryParse(idStr, out var id) && id >= newClientId)
                        newClientId = id + 1;
                }

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
                return newClientId.ToString();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
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
            /// <summary>Ключ — Id поля (OrganizationField), значение — вvведённое значение</summary>
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

            var clientIds = childrenViewData.Select(c => c.Id).ToList();
            var invoicesForClients = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .OrderByDescending(i => i.DateCreated)
                .Select(i => new { i.Client, i.Id })
                .ToListAsync();
            var firstInvoiceIdByClient = invoicesForClients
                .GroupBy(i => i.Client!)
                .ToDictionary(g => g.Key, g => g.First().Id);

            ViewBag.Search = search;
            ViewBag.ClientTotalBalanceByClientId = totalBalanceByClient;
            ViewBag.FirstInvoiceIdByClientId = firstInvoiceIdByClient;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DebtorsOnly = debtorsOnly;
            ViewBag.OrganizationFields = organizationFields;
            ViewBag.ChildrenData = childrenViewData;

            var orgSettings = await _db.OrganizationSettings
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);
            ViewBag.AllowedHassameaccount = orgSettings?.AllowedHassameaccount ?? false;

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

            // Получаем дополнительные поля (с FieldId для формы редактирования)
            var additionalFields = client.OrganizationClientsAdditionalFields
                .Where(af => af.FieldNavigation != null)
                .Select(af => new
                {
                    FieldId = af.Field,
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
                    logo = client.ClientLogo,
                    orgClientGroupId = client.OrgClientGroupId,
                    clientWa = client.ClientWa,
                    clientTg = client.ClientTg
                },
                additionalFields = additionalFields
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

                var client = await _db.OrganizationClients
                    .Include(c => c.OrganizationClientsAdditionalFields)
                    .FirstOrDefaultAsync(c => c.Id == request.ClientId && c.Organization == organizationId);
                if (client == null)
                    return Json(new { success = false, message = "Клиент не найден." });

                client.ClientName = request.ClientName ?? client.ClientName;
                client.ClientInn = request.ClientInn ?? client.ClientInn;
                client.OrgClientGroupId = string.IsNullOrWhiteSpace(request.OrgClientGroupId) ? null : request.OrgClientGroupId;
                client.ClientPhone = request.ClientPhone ?? client.ClientPhone;
                client.ClientAddress = request.ClientAdres ?? client.ClientAddress;
                client.ClientEmail = request.ClientEmail ?? client.ClientEmail;
                client.ClientWa = request.ClientWa ?? client.ClientWa;
                client.ClientTg = request.ClientTg ?? client.ClientTg;
                client.UpdatedDate = DateTime.Now;
                client.ClientStatus = request.ClientStatus ?? client.ClientStatus ?? 1;

                if (request.AdditionalFields != null)
                {
                    var existing = client.OrganizationClientsAdditionalFields.ToList();
                    foreach (var af in existing)
                        _db.OrganizationClientsAdditionalFields.Remove(af);
                    var orgFieldIds = await _db.OrganizationFields
                        .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
                        .Select(f => f.Id)
                        .ToListAsync();
                    foreach (var kv in request.AdditionalFields)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key) || !orgFieldIds.Contains(kv.Key)) continue;
                        _db.OrganizationClientsAdditionalFields.Add(new OrganizationClientsAdditionalField
                        {
                            Id = Guid.NewGuid().ToString(),
                            OrganizationClient = client.Id,
                            Field = kv.Key,
                            Value = (kv.Value ?? "").Trim()
                        });
                    }
                }

                await _db.SaveChangesAsync();
                return Json(new { success = true, message = "Профиль обновлён." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class UpdateChildRequest
        {
            public string ClientId { get; set; } = null!;
            public string? ClientName { get; set; }
            public string? ClientInn { get; set; }
            public string? OrgClientGroupId { get; set; }
            public string? ClientPhone { get; set; }
            public string? ClientAdres { get; set; }
            public string? ClientEmail { get; set; }
            public string? ClientWa { get; set; }
            public string? ClientTg { get; set; }
            public int? ClientStatus { get; set; }
            public Dictionary<string, string>? AdditionalFields { get; set; }
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
        public async Task<IActionResult> Payments(string dateFrom = "", string dateTo = "", List<string>? agentIds = null, List<string>? clientIds = null)
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

            var selectedAgentIds = agentIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
                .Select(id => id!.Trim())
                .ToList() ?? new List<string>();
            if (selectedAgentIds.Count > 0)
                query = query.Where(t => t.Agent != null && selectedAgentIds.Contains(t.Agent));

            var selectedClientIds = clientIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.Trim() != "__all__")
                .Select(id => id!.Trim())
                .ToList() ?? new List<string>();
            if (selectedClientIds.Count > 0)
                query = query.Where(t => t.InvoiceNavigation != null && t.InvoiceNavigation.Client != null && selectedClientIds.Contains(t.InvoiceNavigation.Client));

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

            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SelectedAgentIds = selectedAgentIds;
            ViewBag.SelectedClientIds = selectedClientIds;
            ViewBag.AgentsList = agentsList;
            ViewBag.ClientsList = clientsList;
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
            /// <summary>Создать счёт с флагом «общий лицевой счёт» (новый PayCode, но Hassameaccount=true).</summary>
            public bool Hassameaccount { get; set; }
        }

        public class CreateInvoiceServiceItem
        {
            public string? ServiceId { get; set; }
            public int? Qty { get; set; }
        }
    }
}
