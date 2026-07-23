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

        var sumVisibility = DashboardSumPermissions.GetVisibility(HttpContext);
        var transactions = await DashboardSumPermissions.ApplyDashboardSumFilter(
                _db.Transactions.Where(t =>
                    t.TransactionStatus == "success" &&
                    t.Invoice != null &&
                    invoiceIds.Contains(t.Invoice) &&
                    t.Summ.HasValue),
                sumVisibility)
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

        return View("~/Views/Cabinet/Dashboards/_DetsadDashboard.cshtml");
    }

    private async Task<IActionResult> RenderMedclinicDashboardAsync(string organizationId)
    {
        var sumVisibility = DashboardSumPermissions.GetVisibility(HttpContext);
        var today = DateTime.Today;
        var currentMonth = today.Month;
        var currentYear = today.Year;
        var todayEnd = today.AddDays(1);

        var todayAppointments = await _db.Appointments
            .AsNoTracking()
            .Where(a =>
                a.OrganizationId == organizationId &&
                a.IsActive &&
                a.StartsAt >= today &&
                a.StartsAt < todayEnd)
            .Select(a => new { a.PatientId, a.StartsAt, a.EndsAt, a.PaymentType })
            .ToListAsync();

        var paidSlotsToday = await _db.InvoicePayments
            .AsNoTracking()
            .Where(p =>
                p.PaymentStatus == "paid" &&
                p.DateFrom.HasValue &&
                p.DateFrom.Value >= today &&
                p.DateFrom.Value < todayEnd)
            .Where(p =>
                p.InvoiceNavigation != null &&
                p.InvoiceNavigation.FromAppointments &&
                p.InvoiceNavigation.ClientNavigation != null &&
                p.InvoiceNavigation.ClientNavigation.Organization == organizationId)
            .Select(p => new
            {
                PatientId = p.InvoiceNavigation!.Client,
                StartsAt = p.DateFrom!.Value,
                EndsAt = p.DateTo ?? p.DateFrom!.Value
            })
            .ToListAsync();

        static bool IsPaidAppointmentPayment(string? paymentType)
        {
            var n = (paymentType ?? string.Empty).Trim().ToLowerInvariant();
            return n is "online" or "insurance" or "invoice_paid" or "paid" or "qr_secore_paid";
        }

        var acceptedToday = todayAppointments.Count(a =>
        {
            if (IsPaidAppointmentPayment(a.PaymentType))
                return true;
            if (string.IsNullOrWhiteSpace(a.PatientId))
                return false;
            return paidSlotsToday.Any(p =>
                p.PatientId == a.PatientId &&
                p.StartsAt == a.StartsAt &&
                p.EndsAt == a.EndsAt);
        });

        var totalToday = todayAppointments.Count;
        var remainingToday = Math.Max(0, totalToday - acceptedToday);

        string? newAppointmentUrl = null;
        var showNewAppointment = false;
        if (PermissionHelper.HasPermission(HttpContext, "appointments.registry.view"))
        {
            showNewAppointment = true;
            newAppointmentUrl = Url.Action("Registry", "Appointments");
        }
        else if (PermissionHelper.HasPermission(HttpContext, "appointments.doctor.view"))
        {
            showNewAppointment = true;
            newAppointmentUrl = Url.Action("Doctor", "Appointments");
        }
        else if (PermissionHelper.HasPermission(HttpContext, "appointments.edit"))
        {
            showNewAppointment = true;
            newAppointmentUrl = Url.Action("Registry", "Appointments");
        }

        var transactionsBaseQuery = _db.Transactions
            .AsNoTracking()
            .Where(x =>
                x.InvoiceNavigation != null &&
                x.InvoiceNavigation.ClientNavigation != null &&
                x.InvoiceNavigation.ClientNavigation.Organization == organizationId &&
                x.TransactionStatus == "success");

        var transactionsQuery = DashboardSumPermissions.ApplyDashboardSumFilter(transactionsBaseQuery, sumVisibility);

        var todayTransactionsSumTyiyn = sumVisibility.CanViewAny
            ? await transactionsQuery
                .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value >= today && t.TransactionDate.Value < todayEnd)
                .SumAsync(t => (decimal?)t.Summ) ?? 0m
            : 0m;

        var recentTransactions = sumVisibility.CanViewAny
            ? await transactionsQuery
                .OrderByDescending(x => x.TransactionDate)
                .Take(20)
                .Select(x => new MedclinicDashboardTransactionViewModel
                {
                    Id = x.Id,
                    PatientName = x.InvoiceNavigation!.ClientNavigation!.ClientName ?? "—",
                    Description = x.InvoiceNavigation.NameInvoice ?? "Платёж",
                    TransactionDate = x.TransactionDate,
                    AmountSom = (x.Summ ?? 0m) / 100m,
                    IsCredit = x.TransactionType == "credit",
                    KindLabel = x.TransactionType == "credit" ? "Возврат" : "Оплата",
                    StatusLabel = x.TransactionType == "credit" ? "Возврат" : "Успешно"
                })
                .ToListAsync()
            : new List<MedclinicDashboardTransactionViewModel>();

        var weekStart = today.AddDays(-6);
        var firstDay = new DateTime(currentYear, currentMonth, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var dailyChartStart = weekStart < firstDay ? weekStart : firstDay;
        var dailyChartEndExclusive = lastDay.AddDays(1);

        var dailySumsByDate = new Dictionary<DateTime, decimal>();
        var monthlySumsByMonth = new Dictionary<int, decimal>();
        if (sumVisibility.CanViewAny)
        {
            var dailySums = await transactionsQuery
                .Where(t =>
                    t.TransactionDate.HasValue &&
                    t.TransactionDate.Value >= dailyChartStart &&
                    t.TransactionDate.Value < dailyChartEndExclusive)
                .GroupBy(t => t.TransactionDate!.Value.Date)
                .Select(g => new { Date = g.Key, Sum = g.Sum(t => ((decimal?)t.Summ) ?? 0m) / 100m })
                .ToListAsync();
            dailySumsByDate = dailySums.ToDictionary(x => x.Date, x => x.Sum);

            var monthlySums = await transactionsQuery
                .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == currentYear)
                .GroupBy(t => t.TransactionDate!.Value.Month)
                .Select(g => new { Month = g.Key, Sum = g.Sum(t => ((decimal?)t.Summ) ?? 0m) / 100m })
                .ToListAsync();
            monthlySumsByMonth = monthlySums.ToDictionary(x => x.Month, x => x.Sum);
        }

        var chartWeek = new List<object>();
        for (var dayShift = 6; dayShift >= 0; dayShift--)
        {
            var date = today.AddDays(-dayShift);
            chartWeek.Add(new { label = date.ToString("dd.MM"), value = dailySumsByDate.GetValueOrDefault(date, 0m) });
        }

        var chartMonth = new List<object>();
        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
            chartMonth.Add(new { label = date.ToString("dd.MM"), value = dailySumsByDate.GetValueOrDefault(date, 0m) });

        var chartYear = new List<object>();
        for (var month = 1; month <= 12; month++)
        {
            chartYear.Add(new
            {
                label = new DateTime(currentYear, month, 1).ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),
                value = monthlySumsByMonth.GetValueOrDefault(month, 0m)
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
            OrganizationName = organization?.Name ?? "Medclinic",
            TodayTitle = today.ToString("dddd, d MMMM", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),
            AppointmentsTotalToday = totalToday,
            AppointmentsAcceptedToday = acceptedToday,
            AppointmentsRemainingToday = remainingToday,
            ShowNewAppointmentButton = showNewAppointment,
            NewAppointmentUrl = newAppointmentUrl,
            CanViewPaymentSums = sumVisibility.CanViewAny,
            TodayTransactionsSumSom = todayTransactionsSumTyiyn / 100m,
            Transactions = recentTransactions,
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
