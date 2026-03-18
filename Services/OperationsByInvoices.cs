using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services;

public class OperationsByInvoices
{
    private readonly AppDbContext _db;
    private readonly TransactionCommissionService _commissionService;

    public OperationsByInvoices(AppDbContext db, TransactionCommissionService commissionService)
    {
        _db = db;
        _commissionService = commissionService;
    }

    /// <summary>
    /// Получаем все платежи, которые уже должны быть оплачены
    /// (non_paid + дата наступила)
    /// Сортировка: сначала по дате, потом по сумме
    /// </summary>
    public IEnumerable<InvoicePayment> GetDuePayments(Invoice invoice)
    {
        return invoice.InvoicePayments
            .Where(p => p.PaymentStatus == "non_paid"
                     && p.DateFrom.HasValue
                     && p.DateFrom.Value.Date <= DateTime.Today)
            .OrderBy(p => p.DateFrom.Value)
            .ThenBy(p => p.PaymentSumm ?? 0);
    }

    /// <summary>
    /// Получаем все платежи, которые  должны быть оплачены завтра
    /// (non_paid + дата наступит завтра)
    /// Сортировка: сначала по дате, потом по сумме
    /// </summary>
    public IEnumerable<InvoicePayment> GetDuePaymentsbyPlan(Invoice invoice)
    {
        return invoice.InvoicePayments
            .Where(p => p.PaymentStatus == "non_paid"
                     && p.DateFrom.HasValue
                     && p.DateFrom.Value.Date == DateTime.Today.AddDays(1))
            .OrderBy(p => p.DateFrom.Value)
            .ThenBy(p => p.PaymentSumm ?? 0);
    }

    /// <summary>
    /// Получаем все платежи, с ближайщими оплатами
    /// (non_paid + дата наступит через день и выше)
    /// Сортировка: сначала по дате, потом по сумме
    /// </summary>
    public IEnumerable<InvoicePayment> GetDuePaymentsFuture(Invoice invoice)
    {
        return invoice.InvoicePayments
            .Where(p => p.PaymentStatus == "non_paid"
                     && p.DateFrom.HasValue
                     && p.DateFrom.Value.Date > DateTime.Today.AddDays(1))
            .OrderBy(p => p.DateFrom.Value)
            .ThenBy(p => p.PaymentSumm ?? 0);
    }

    public string BuildInvoicesText(IEnumerable<InvoicePayment> payments)
    {
        return string.Join("; ",
            payments
                .GroupBy(p => p.InvoiceNavigation)
                .Select(g =>
                {
                    var total = g.Sum(p => p.PaymentSumm ?? 0);

                    return $"{g.Key.NameInvoice}: " +
                           $"за {string.Join(", ", g.Select(p => p.PeriodValue))}: " +
                           $"Сумма(KGS): {ParsersHelper.ToMoneyStringFromCents(total)}";
                }));
    }

    /// <summary>
    /// Создание записи о транзакции. Три комиссии: не переданные сохраняются как 0.
    /// </summary>
    public Transaction CreateTransaction(
        string? invoiceId,
        string? paymentInvId,
        Agent agent,
        decimal? sum,
        decimal? sumWithFee,
        string txnId,
        string transactionType,
        decimal? lowerCommissionFromOrg = null,
        decimal? upperCommissionFromAgent = null,
        decimal? lowerCommissionToAgent = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            Invoice = invoiceId,
            PaymentInvoice = paymentInvId,
            Agent = agent.Id,
            Summ = sum,
            TransactionSumm = sumWithFee,
            LowerCommissionFromOrg = lowerCommissionFromOrg ?? 0,
            UpperCommissionFromAgent = upperCommissionFromAgent ?? 0,
            LowerCommissionToAgent = lowerCommissionToAgent ?? 0,
            TransactionDate = DateTime.Now,
            TransactionStatus = "success",
            TransactionType = transactionType,
            TxnId = txnId,
            TransactionSystem = "secore"
        };
    }

    /// <summary>
    /// Создаёт транзакцию с расчётом всех включённых комиссий (нижняя от организации, верхняя от агента, нижняя к агенту).
    /// </summary>
    public async Task<Transaction> CreateTransactionWithCommissionAsync(
        string? invoiceId,
        string? paymentInvId,
        Agent agent,
        decimal? sum,
        decimal? sumWithFee,
        string txnId,
        string transactionType,
        CancellationToken cancellationToken = default)
    {
        decimal lowerOrg = 0, upperAgent = 0, lowerAgent = 0;
        if (!string.IsNullOrEmpty(invoiceId) && sum.HasValue && sum > 0)
        {
            var invoice = await _db.Invoices
                .AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => new { i.Client })
                .FirstOrDefaultAsync(cancellationToken);
            var orgId = invoice?.Client != null
                ? (await _db.OrganizationClients
                    .AsNoTracking()
                    .Where(c => c.Id == invoice.Client)
                    .Select(c => c.Organization)
                    .FirstOrDefaultAsync(cancellationToken))
                : null;
            var commissions = await _commissionService.GetCommissionsForTransactionAsync(orgId, agent.Id, sum.Value, cancellationToken);
            lowerOrg = commissions.LowerCommissionFromOrg;
            upperAgent = commissions.UpperCommissionFromAgent;
            lowerAgent = commissions.LowerCommissionToAgent;
        }
        return CreateTransaction(
            invoiceId, paymentInvId, agent, sum, sumWithFee, txnId, transactionType,
            lowerOrg, upperAgent, lowerAgent);
    }

    /// <summary>
    /// Погашение платежей в рамках одного или нескольких инвойсов.
    /// Возвращает остаток средств. Комиссия по агенту рассчитывается и записывается в транзакцию.
    /// </summary>
    public async Task<decimal?> PayPaymentsAsync(
        IEnumerable<InvoicePayment> payments,
        decimal? amount,
        Agent agent,
        string txnId,
        CancellationToken cancellationToken = default)
    {
        foreach (var payment in payments)
        {
            var paymentSumm = payment.PaymentSumm ?? 0;
            if (amount < paymentSumm)
                break;
            amount -= paymentSumm;
            payment.PaymentStatus = "paid";
            var trn = await CreateTransactionWithCommissionAsync(
                payment.Invoice, payment.Id, agent, paymentSumm, paymentSumm, txnId, "payPaymentInvoice", cancellationToken);
            _db.Transactions.Add(trn);
        }
        return amount;
    }

    /// <summary>
    /// Применяет к сущности Invoice данные из запроса обновления: поля счёта и пересчёт услуг (ручная сумма или список услуг).
    /// Не вызывает SaveChanges — сохраняет вызывающий код.
    /// </summary>
    public async Task ApplyInvoiceUpdateAsync(Invoice invoice, UpdateInvoiceRequest request, string organizationId, CancellationToken cancellationToken = default)
    {
        invoice.NameInvoice = request.NameInvoice ?? invoice.NameInvoice;
        invoice.Client = request.Client ?? invoice.Client;
        invoice.InvoiceStatus = request.InvoiceStatus ?? "actual";
        invoice.Periodicity = request.Periodicity ?? "monthly";
        invoice.DateStartInvoice = request.DateStartInvoice;
        invoice.DateEndInvoice = request.DateEndInvoice;
        invoice.AutoProlongation = request.AutoProlongation;
        invoice.NextStartInvoice = request.NextStartInvoice;
        invoice.PayCode = request.PayCode ?? invoice.PayCode;
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
                .ToDictionaryAsync(s => s.Id, cancellationToken);
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
    }

    /// <summary>
    /// Создаёт счета по списку клиентов: инвойс, услуги, периоды платежей.
    /// Генерация PayCode, даты и периоды — внутри. Делает SaveChanges.
    /// </summary>
    /// <returns>Список Id созданных инвойсов.</returns>
    public async Task<List<string>> CreateInvoicesAsync(CreateInvoicesInput input, CancellationToken cancellationToken = default)
    {
        var organizationId = input.OrganizationId;
        var userId = input.UserId;
        var clients = input.Clients;
        var useManualService = input.UseManualService;
        var orgServices = input.OrgServices;
        var requestPeriodicity = input.Periodicity ?? "monthly";
        var useExistingPayCode = !string.IsNullOrWhiteSpace(input.PayCode);

        var orgPrefix = organizationId;
        var prefixLen = orgPrefix.Length;
        var existingPayCodes = await _db.Invoices
            .Where(i => i.Client != null && i.ClientNavigation != null && i.ClientNavigation.Organization == organizationId && i.PayCode != null)
            .Select(i => i.PayCode)
            .ToListAsync(cancellationToken);

        long maxCounter = 0;
        foreach (var pc in existingPayCodes)
        {
            if (pc != null && pc.Length == prefixLen + 9 && pc.StartsWith(orgPrefix) && long.TryParse(pc.Substring(prefixLen), out var c) && c > maxCounter)
                maxCounter = c;
        }

        var createdIds = new List<string>();
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

        foreach (var client in clients)
        {
            var payCode = useExistingPayCode ? input.PayCode!.Trim() : (orgPrefix + (++maxCounter).ToString("D9"));
            var invoiceId = Guid.NewGuid().ToString();

            var dateStart = input.DateStartInvoice ?? DateTime.Today;
            if (dateStart.Kind != DateTimeKind.Unspecified)
                dateStart = DateTime.SpecifyKind(dateStart.Kind == DateTimeKind.Utc ? dateStart.ToLocalTime() : dateStart, DateTimeKind.Unspecified);

            var autoProlongation = input.AutoProlongation;
            DateTime? dateEnd = null;
            if (!autoProlongation && input.DateEndInvoice.HasValue)
            {
                dateEnd = input.DateEndInvoice.Value;
                if (dateEnd.Value.Kind != DateTimeKind.Unspecified)
                    dateEnd = DateTime.SpecifyKind(dateEnd.Value.Kind == DateTimeKind.Utc ? dateEnd.Value.ToLocalTime() : dateEnd.Value, DateTimeKind.Unspecified);
            }

            decimal totalTyiyn;
            if (useManualService)
            {
                totalTyiyn = (decimal)(input.ManualServicePriceSom!.Value * 100m);
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
                foreach (var item in input.ServiceItems ?? Enumerable.Empty<CreateInvoiceServiceItemInput>())
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

            var periodicity = requestPeriodicity;

            var invoice = new Invoice
            {
                Id = invoiceId,
                DateCreated = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                UserCreater = userId,
                InvoiceStatus = "actual",
                Periodicity = periodicity,
                DateStartInvoice = dateStart,
                DateEndInvoice = dateEnd,
                Balance = 0,
                FixedSumm = totalTyiyn,
                PayCode = payCode,
                Client = client.Id,
                AutoProlongation = autoProlongation,
                Hassameaccount = input.Hassameaccount || useExistingPayCode,
                NameInvoice = !string.IsNullOrWhiteSpace(input.NameInvoice) ? input.NameInvoice : "Счет " + (client.ClientName ?? client.Id)
            };
            _db.Invoices.Add(invoice);

            if (autoProlongation)
            {
                DateTime dateFrom;
                DateTime dateTo;
                string periodValue;
                if (input.UseCurrentDateTime)
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
                    var periodStart = input.UseCurrentDateTime ? dateStart : GetPeriodStart(dateStart, "weekly");
                    while (periodStart < dateEnd.Value)
                    {
                        var periodEnd = input.UseCurrentDateTime
                            ? new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 23, 59, 59, DateTimeKind.Unspecified).AddDays(6)
                            : GetPeriodEnd(periodStart, "weekly");
                        var dateTo2 = periodEnd > dateEnd.Value ? dateEnd.Value : periodEnd;
                        var periodValue = periodStart.ToString("dd.MM.yyyy", ru) + " - " + dateTo2.ToString("dd.MM.yyyy", ru);
                        _db.InvoicePayments.Add(new InvoicePayment
                        {
                            Id = Guid.NewGuid().ToString(),
                            Invoice = invoiceId,
                            DateFrom = periodStart,
                            DateTo = dateTo2,
                            PaymentStatus = "non_paid",
                            PeriodValue = periodValue,
                            PaymentSumm = totalTyiyn
                        });
                        periodStart = periodStart.AddDays(7);
                        if (!input.UseCurrentDateTime)
                            periodStart = new DateTime(periodStart.Year, periodStart.Month, periodStart.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    }
                }
                else if (periodicity == "yearly")
                {
                    if (input.UseCurrentDateTime)
                    {
                        var periodStart = dateStart;
                        while (periodStart < dateEnd.Value)
                        {
                            var periodEnd = new DateTime(periodStart.Year, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
                            var dateTo2 = periodEnd > dateEnd.Value ? dateEnd.Value : periodEnd;
                            _db.InvoicePayments.Add(new InvoicePayment
                            {
                                Id = Guid.NewGuid().ToString(),
                                Invoice = invoiceId,
                                DateFrom = periodStart,
                                DateTo = dateTo2,
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
                            var dateTo2 = new DateTime(y, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
                            _db.InvoicePayments.Add(new InvoicePayment
                            {
                                Id = Guid.NewGuid().ToString(),
                                Invoice = invoiceId,
                                DateFrom = dateFrom,
                                DateTo = dateTo2,
                                PaymentStatus = "non_paid",
                                PeriodValue = y.ToString(),
                                PaymentSumm = totalTyiyn
                            });
                        }
                    }
                }
                else
                {
                    if (input.UseCurrentDateTime)
                    {
                        var periodStart = dateStart;
                        while (periodStart < dateEnd.Value)
                        {
                            var periodEnd = periodStart.AddMonths(1).AddSeconds(-1);
                            var dateTo2 = periodEnd > dateEnd.Value ? dateEnd.Value : periodEnd;
                            var periodValue = ru.DateTimeFormat.GetMonthName(periodStart.Month) + " " + periodStart.Year;
                            _db.InvoicePayments.Add(new InvoicePayment
                            {
                                Id = Guid.NewGuid().ToString(),
                                Invoice = invoiceId,
                                DateFrom = periodStart,
                                DateTo = dateTo2,
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
                        for (var d = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
                             d.Year < endYear || (d.Year == endYear && d.Month <= endMonth);
                             d = d.AddMonths(1))
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

        await _db.SaveChangesAsync(cancellationToken);
        return createdIds;
    }
}

