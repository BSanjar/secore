using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Cabinet;

namespace WebApplication1.Controllers;

[RequireAuth]
public class CabinetController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _currentTenantService;

    public CabinetController(AppDbContext db, ICurrentTenantService currentTenantService)
    {
        _db = db;
        _currentTenantService = currentTenantService;
    }

    [RequirePermission("dashboard.view")]
    public async Task<IActionResult> Index()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
        {
            return Unauthorized();
        }

        return tenant.Profile.Key switch
        {
            "simple" => await RenderSimpleDashboardAsync(tenant.OrganizationId),
            "detsad" => await RenderDetsadDashboardAsync(tenant.OrganizationId),
            "medclinic" => await RenderMedclinicDashboardAsync(tenant.OrganizationId),
            _ => await RenderStandartDashboardAsync(tenant.OrganizationId)
        };
    }

    [RequirePermission("transactions.view")]
    public async Task<IActionResult> PaymentHistory()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
        {
            return Unauthorized();
        }

        if (!string.Equals(tenant.Profile.Key, "standart", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var transactions = await _db.Transactions
            .AsNoTracking()
            .Include(t => t.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
            .Where(t => t.InvoiceNavigation != null &&
                        t.InvoiceNavigation.ClientNavigation != null &&
                        t.InvoiceNavigation.ClientNavigation.Organization == tenant.OrganizationId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        return View("~/Views/Cabinet/PaymentHistory.cshtml", transactions);
    }

    [RequirePermission("invoices.view")]
    public async Task<IActionResult> Invoices()
    {
        var tenant = _currentTenantService.GetCurrent();
        if (string.IsNullOrWhiteSpace(tenant.OrganizationId))
        {
            return Unauthorized();
        }

        if (!string.Equals(tenant.Profile.Key, "standart", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.ClientNavigation)
            .Include(i => i.InvoiceServices)
                .ThenInclude(s => s.ServiceNavigation)
            .Where(i => i.ClientNavigation != null &&
                        i.ClientNavigation.Organization == tenant.OrganizationId)
            .OrderByDescending(i => i.DateCreated)
            .ToListAsync();

        return View("~/Views/Cabinet/Invoices.cshtml", invoices);
    }

    private async Task<IActionResult> RenderStandartDashboardAsync(string organizationId)
    {
        var transactions = await _db.Transactions
            .AsNoTracking()
            .Include(t => t.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
            .Where(t => t.TransactionStatus == "success" &&
                        t.InvoiceNavigation != null &&
                        t.InvoiceNavigation.ClientNavigation != null &&
                        t.InvoiceNavigation.ClientNavigation.Organization == organizationId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var totalIncome = transactions
            .Where(t => t.Summ.HasValue && t.TransactionType == "debit")
            .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

        var today = DateTime.Today;
        var now = DateTime.Now;

        ViewBag.TotalIncome = totalIncome;
        ViewBag.TotalTransactions = transactions.Count;
        ViewBag.TodayIncome = transactions
            .Where(t => t.TransactionDate.HasValue &&
                        t.TransactionDate.Value.Date == today &&
                        t.Summ.HasValue &&
                        t.TransactionType == "debit")
            .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;
        ViewBag.ThisMonthIncome = transactions
            .Where(t => t.TransactionDate.HasValue &&
                        t.TransactionDate.Value.Month == now.Month &&
                        t.TransactionDate.Value.Year == now.Year &&
                        t.Summ.HasValue &&
                        t.TransactionType == "debit")
            .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;

        return View("~/Views/Cabinet/Dashboards/_StandartDashboard.cshtml");
    }

    private async Task<IActionResult> RenderSimpleDashboardAsync(string organizationId)
    {
        var transactions = await _db.Transactions
            .AsNoTracking()
            .Include(t => t.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
            .Where(t => t.TransactionType == "debit" &&
                        t.TransactionStatus == "success" &&
                        t.InvoiceNavigation != null &&
                        t.InvoiceNavigation.ClientNavigation != null &&
                        t.InvoiceNavigation.ClientNavigation.Organization == organizationId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(10)
            .ToListAsync();

        var today = DateTime.Today;
        var now = DateTime.Now;

        ViewBag.TodayIncome = transactions
            .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Date == today)
            .Sum(t => (t.Summ ?? 0) / 100m);

        ViewBag.ThisMonthIncome = transactions
            .Where(t => t.TransactionDate.HasValue &&
                        t.TransactionDate.Value.Month == now.Month &&
                        t.TransactionDate.Value.Year == now.Year)
            .Sum(t => (t.Summ ?? 0) / 100m);

        ViewBag.TotalTransactions = await _db.Transactions
            .AsNoTracking()
            .Include(t => t.InvoiceNavigation)
                .ThenInclude(i => i!.ClientNavigation)
            .CountAsync(t => t.InvoiceNavigation != null &&
                             t.InvoiceNavigation.ClientNavigation != null &&
                             t.InvoiceNavigation.ClientNavigation.Organization == organizationId);

        return View("~/Views/Cabinet/Dashboards/_SimpleDashboard.cshtml", transactions);
    }

    private async Task<IActionResult> RenderDetsadDashboardAsync(string organizationId)
    {
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

        var chartWeek = new List<object>();
        for (var dayShift = 6; dayShift >= 0; dayShift--)
        {
            var date = today.AddDays(-dayShift);
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
        for (var month = 1; month <= 12; month++)
        {
            var sum = transactions
                .Where(t => t.TransactionDate.HasValue &&
                            t.TransactionDate.Value.Month == month &&
                            t.TransactionDate.Value.Year == currentYear)
                .Sum(t => (decimal)(t.Summ ?? 0)) / 100m;
            chartYear.Add(new
            {
                label = new DateTime(currentYear, month, 1).ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),
                value = sum
            });
        }

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

        var debtorsList = clients
            .Where(c => balanceByClient.GetValueOrDefault(c.Id, 0m) < 0)
            .OrderBy(c => balanceByClient.GetValueOrDefault(c.Id, 0m))
            .Take(10)
            .Select(c => new
            {
                ClientName = c.ClientName ?? "-",
                ClientId = c.Id,
                DebtAmount = Math.Abs(balanceByClient.GetValueOrDefault(c.Id, 0m)) / 100m,
                Status = c.ClientStatus == 1 ? "Активный" : "Приостановлен"
            })
            .ToList();

        var recentInvoices = invoices
            .Where(i => i.InvoiceStatus == "actual")
            .OrderByDescending(i => i.DateCreated ?? DateTime.MinValue)
            .Take(10)
            .Select(i => new
            {
                i.Id,
                Name = i.NameInvoice ?? "Без названия",
                Balance = (i.Balance ?? 0m) / 100m,
                Status = i.InvoiceStatus == "actual" ? "Активный" : i.InvoiceStatus ?? "-",
                ClientName = i.ClientNavigation?.ClientName
            })
            .ToList();

        ViewBag.MonthIncome = monthIncome;
        ViewBag.YearIncome = yearIncome;
        ViewBag.TotalClients = clients.Count;
        ViewBag.ActiveInvoices = invoices.Count(i => i.InvoiceStatus == "actual");
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

        return View("~/Views/Cabinet/Dashboards/_DetsadDashboard.cshtml");
    }

    private async Task<IActionResult> RenderMedclinicDashboardAsync(string organizationId)
    {
        var today = DateTime.Today;
        var currentMonth = today.Month;
        var currentYear = today.Year;

        var todayAcceptedPatients = await _db.Transactions
            .AsNoTracking()
            .Where(x =>
                x.TransactionStatus == "success" &&
                x.TransactionDate.HasValue &&
                x.TransactionDate.Value.Date == today &&
                x.InvoiceNavigation != null &&
                x.InvoiceNavigation.ClientNavigation != null &&
                x.InvoiceNavigation.ClientNavigation.Organization == organizationId)
            .Select(x => x.InvoiceNavigation!.ClientNavigation!.Id)
            .Distinct()
            .CountAsync();

        var doctorUserIds = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => (ur.Isdeleted == null || ur.Isdeleted == 0) && ur.User != null && ur.Role != null)
            .Join(
                _db.Roles.AsNoTracking()
                    .Where(r => (r.Isdeleted == null || r.Isdeleted == 0) &&
                                r.Organization == organizationId &&
                                r.Name != null &&
                                (EF.Functions.ILike(r.Name, "%doctor%") || EF.Functions.ILike(r.Name, "%врач%"))),
                ur => ur.Role,
                r => r.Id,
                (ur, _) => ur.User!)
            .Distinct()
            .ToListAsync();

        var doctorsCount = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.Organization == organizationId &&
                (x.Isdeleted == null || x.Isdeleted == 0) &&
                doctorUserIds.Contains(x.Id))
            .CountAsync();

        var transactionsQuery = _db.Transactions
            .AsNoTracking()
            .Where(x =>
                x.InvoiceNavigation != null &&
                x.InvoiceNavigation.ClientNavigation != null &&
                x.InvoiceNavigation.ClientNavigation.Organization == organizationId &&
                x.TransactionStatus == "success");

        var transactionsAmount = await transactionsQuery
            .SumAsync(x => (decimal?)x.Summ) ?? 0m;

        var recentTransactions = await transactionsQuery
            .OrderByDescending(x => x.TransactionDate)
            .Take(12)
            .Select(x => new MedclinicDashboardTransactionViewModel
            {
                Id = x.Id,
                PatientName = x.InvoiceNavigation!.ClientNavigation!.ClientName ?? "—",
                Description = x.InvoiceNavigation.NameInvoice ?? "Платёж",
                TransactionDate = x.TransactionDate,
                Amount = (x.Summ ?? 0m) / 100m,
                Status = x.TransactionStatus ?? string.Empty
            })
            .ToListAsync();

        var weekStart = today.AddDays(-6);
        var firstDay = new DateTime(currentYear, currentMonth, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var dailyChartStart = weekStart < firstDay ? weekStart : firstDay;
        var dailyChartEndExclusive = lastDay.AddDays(1);

        var dailySums = await transactionsQuery
            .Where(t =>
                t.TransactionDate.HasValue &&
                t.TransactionDate.Value.Date >= dailyChartStart &&
                t.TransactionDate.Value.Date < dailyChartEndExclusive)
            .GroupBy(t => t.TransactionDate!.Value.Date)
            .Select(g => new
            {
                Date = g.Key,
                Sum = g.Sum(t => ((decimal?)t.Summ) ?? 0m) / 100m
            })
            .ToListAsync();

        var dailySumsByDate = dailySums.ToDictionary(x => x.Date, x => x.Sum);

        var monthlySums = await transactionsQuery
            .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == currentYear)
            .GroupBy(t => t.TransactionDate!.Value.Month)
            .Select(g => new
            {
                Month = g.Key,
                Sum = g.Sum(t => ((decimal?)t.Summ) ?? 0m) / 100m
            })
            .ToListAsync();

        var monthlySumsByMonth = monthlySums.ToDictionary(x => x.Month, x => x.Sum);

        var chartWeek = new List<object>();
        for (var dayShift = 6; dayShift >= 0; dayShift--)
        {
            var date = today.AddDays(-dayShift);
            var sum = dailySumsByDate.GetValueOrDefault(date, 0m);
            chartWeek.Add(new { label = date.ToString("dd.MM"), value = sum });
        }

        var chartMonth = new List<object>();
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var sum = dailySumsByDate.GetValueOrDefault(date, 0m);
            chartMonth.Add(new { label = date.ToString("dd.MM"), value = sum });
        }

        var chartYear = new List<object>();
        for (var month = 1; month <= 12; month++)
        {
            var sum = monthlySumsByMonth.GetValueOrDefault(month, 0m);
            chartYear.Add(new
            {
                label = new DateTime(currentYear, month, 1).ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),
                value = sum
            });
        }

        var organization = await _db.Organizations.FindAsync(organizationId);
        var orgSettings = await _db.OrganizationSettings
            .AsNoTracking()
            .Include(s => s.Commission)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);
        var agentCommission = await _db.AgentCommissions
            .AsNoTracking()
            .Include(ac => ac.Commission)
            .Include(ac => ac.LowerCommission)
            .FirstOrDefaultAsync(ac => ac.OrganizationId == organizationId);

        var commissionName = orgSettings?.Commission?.Name ?? agentCommission?.Commission?.Name;
        var commissionKind = orgSettings?.Commission?.CommissionKind ?? agentCommission?.Commission?.CommissionKind;
        var commissionRate = orgSettings?.Commission?.Rate ?? agentCommission?.Commission?.Rate;
        var commissionFixed = orgSettings?.Commission?.FixedAmount ?? agentCommission?.Commission?.FixedAmount;
        var billingType = orgSettings?.BillingType ?? "commission";
        var hasSubscription = string.Equals(billingType, "subscription", StringComparison.OrdinalIgnoreCase);

        static string CommissionKindText(string? kind)
        {
            if (string.IsNullOrWhiteSpace(kind)) return "—";
            return kind switch
            {
                "percent" => "Процентная",
                "fixed" => "Фиксированная",
                "mixed" => "Смешанная",
                "single_tier" => "Ступенчатая",
                "progressive" => "Прогрессивная",
                _ => kind
            };
        }

        var commissionValue = "—";
        if (commissionRate.HasValue && commissionRate.Value != 0)
            commissionValue = (commissionRate.Value * 100).ToString("0.##") + "%";
        else if (commissionFixed.HasValue && commissionFixed.Value != 0)
            commissionValue = commissionFixed.Value.ToString("N2") + " сом";

        var model = new MedclinicDashboardViewModel
        {
            Metrics = new[]
            {
                new MedclinicDashboardMetricViewModel
                {
                    Label = "Принято сегодня",
                    Value = todayAcceptedPatients.ToString(),
                    Description = "Уникальные пациенты с успешными оплатами за сегодня."
                },
                new MedclinicDashboardMetricViewModel
                {
                    Label = "Докторов",
                    Value = doctorsCount.ToString(),
                    Description = "Активные пользователи клиники в текущей организации."
                },
                new MedclinicDashboardMetricViewModel
                {
                    Label = "Сумма транзакций",
                    Value = $"{transactionsAmount / 100m:N2} c",
                    Description = "Успешные транзакции клиники за всё время."
                }
            },
            Transactions = recentTransactions,
            OrganizationName = organization?.Name ?? "Medclinic",
            HasSubscription = hasSubscription,
            BillingStatus = hasSubscription ? "Активна" : "Комиссионный",
            CommissionKindText = CommissionKindText(commissionKind),
            CommissionValueText = commissionValue,
            CommissionModelName = commissionName ?? "—",
            ChartWeekJson = System.Text.Json.JsonSerializer.Serialize(chartWeek),
            ChartMonthJson = System.Text.Json.JsonSerializer.Serialize(chartMonth),
            ChartYearJson = System.Text.Json.JsonSerializer.Serialize(chartYear)
        };

        return View("~/Views/Cabinet/Dashboards/_MedclinicDashboard.cshtml", model);
    }
}
