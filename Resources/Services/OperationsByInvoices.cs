using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Models.JsonApiModels;
using WebApplication1.Models.WebApiModels;

namespace WebApplication1.Services;

public class OperationsByInvoices
{
    public const string AppointmentPaymentQrSecorePaid = "qr_secore_paid";

    public const int PayCodeOrganizationDigits = 5;
    public const int PayCodeCounterDigits = 9;
    public const int PayCodeTotalLength = PayCodeOrganizationDigits + PayCodeCounterDigits;

    private readonly AppDbContext _db;
    private readonly TransactionCommissionService _commissionService;
    private readonly InvoiceQrService _invoiceQrService;

    public class ApiInvoicesContext
    {
        public ErrorCode? ErrorCode { get; set; }
        public Organization? Organization { get; set; }
        public List<Invoice> Invoices { get; set; } = new();
    }

    public class ApiPaymentApplyResult
    {
        public List<InvoicePayment> PaidPayments { get; set; } = new();
        public decimal PaidSum { get; set; }
        public decimal BalanceAdded { get; set; }
        public decimal Rest { get; set; }
        public Invoice FirstInvoice { get; set; } = null!;
    }

    public class DebtInfoResult
    {
        public List<InvoicePayment> DuePayments { get; set; } = new();
        public List<InvoicePayment> PlanPayments { get; set; } = new();
        public List<InvoicePayment> FuturePayments { get; set; } = new();
        public List<JsonInvoiceForPaymentItem> Items { get; set; } = new();
        public decimal RecommendedSum { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool HasDebt => DuePayments.Any() || PlanPayments.Any() || FuturePayments.Any();
    }

    public OperationsByInvoices(AppDbContext db, TransactionCommissionService commissionService, InvoiceQrService invoiceQrService)
    {
        _db = db;
        _commissionService = commissionService;
        _invoiceQrService = invoiceQrService;
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

    public string BuildPaidInvoicesText(IEnumerable<InvoicePayment> payments)
    {
        var paymentList = payments.ToList();
        if (!paymentList.Any())
            return string.Empty;

        return string.Join("\n",
            paymentList.Select(p =>
                $"Инвойс: {p.InvoiceNavigation?.NameInvoice}, " +
                $"Период: {p.PeriodValue ?? "не указан"}, " +
                $"Сумма: {ParsersHelper.ToMoneyStringFromCents(p.PaymentSumm)} KGS (оплачено)"));
    }

    public string BuildPaySuccessDescription(decimal paidSum, decimal restBalance)
    {
        // restBalance < 0: долг еще остался (не хватает денег)
        if (restBalance < 0)
            return $"Платеж успешно принят. Не хватает {ParsersHelper.ToMoneyStringFromCents(Math.Abs(restBalance))} для погашения долга.";
        // paidSum > 0: что-то погасили по графику/долгам
        if (paidSum > 0)
            return "Платеж успешно принят. Долг погашен/списание выполнено.";

        return "Платеж успешно принят. Баланс пополнен.";
    }

    public async Task<JsonCheckResponse> CreateJsonCheckResponseAsync(
        string serviceId,
        string account,
        CancellationToken cancellationToken = default)
    {
        var context = await GetApiInvoicesContextAsync(serviceId, account, cancellationToken);
        if (context.ErrorCode.HasValue)
            return CreateCheckErrorResponse(context.ErrorCode.Value, account);

        var organization = context.Organization!;
        var invoices = context.Invoices;

        var firstInvoice = invoices.First();

        var client = await _db.OrganizationClients
            .FirstOrDefaultAsync(c => c.Id == firstInvoice.Client, cancellationToken);
        var debtInfo = BuildDebtInfo(invoices);

        return new JsonCheckResponse
        {
            Result = (int)ErrorCode.Success,
            Description = WebApiResponseService.GetErrorDescription(ErrorCode.Success),
            Account = long.TryParse(account, out var accountLong) ? accountLong : 0L,
            BalanceSum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(debtInfo.CurrentBalance), CultureInfo.InvariantCulture),
            RecomendedPaySum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(debtInfo.RecommendedSum), CultureInfo.InvariantCulture),
            Organization = organization.Name ?? string.Empty,
            Subscriber = client?.ClientName ?? string.Empty,
            InvoicesForPayment = debtInfo.Items
        };
    }

    public DebtInfoResult BuildDebtInfo(IEnumerable<Invoice> invoices)
    {
        var invoiceList = invoices.ToList();

        decimal recommendedSum = 0;
        var items = new List<JsonInvoiceForPaymentItem>();

        var duePayments = invoiceList.SelectMany(GetDuePayments).ToList();
        if (duePayments.Any())
        {
            recommendedSum += duePayments.Sum(p => p.PaymentSumm ?? 0);
            items.AddRange(duePayments.Select(ToJsonInvoiceForPaymentItem));
        }

        var planPayments = invoiceList.SelectMany(GetDuePaymentsbyPlan).ToList();
        if (planPayments.Any())
        {
            recommendedSum += planPayments.Sum(p => p.PaymentSumm ?? 0);
            items.AddRange(planPayments.Select(ToJsonInvoiceForPaymentItem));
        }

        var futurePayments = new List<InvoicePayment>();
        if (!duePayments.Any() && !planPayments.Any())
        {
            futurePayments = invoiceList.SelectMany(GetDuePaymentsFuture).ToList();
            if (futurePayments.Any())
            {
                recommendedSum += futurePayments.Sum(p => p.PaymentSumm ?? 0);
                items.AddRange(futurePayments.Select(ToJsonInvoiceForPaymentItem));
            }
        }

        var currentBalance = invoiceList.FirstOrDefault()?.Balance ?? 0m;
        if (currentBalance > 0)
        {
            recommendedSum -= currentBalance;
            if (recommendedSum < 0)
                recommendedSum = 0;
        }

        return new DebtInfoResult
        {
            DuePayments = duePayments,
            PlanPayments = planPayments,
            FuturePayments = futurePayments,
            Items = items,
            RecommendedSum = recommendedSum,
            CurrentBalance = currentBalance
        };
    }

    public async Task<ApiInvoicesContext> GetApiInvoicesContextAsync(
        string serviceId,
        string account,
        CancellationToken cancellationToken = default)
    {
        var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == serviceId, cancellationToken);
        if (organization == null)
            return new ApiInvoicesContext { ErrorCode = ErrorCode.ServiceIdNotFound };

        // Дублируем поведение XML-коннектора: account должен принадлежать organization
        if (account.Length < 5 || account.Substring(0, 5) != organization.Id)
            return new ApiInvoicesContext { ErrorCode = ErrorCode.AccountNotFound };

        var invoices = await _db.Invoices
            .Include(i => i.InvoicePayments)
            .Where(i => i.PayCode == account
                        && i.InvoiceStatus == "actual"
                        && i.ClientNavigation.Organization == organization.Id)
            .ToListAsync(cancellationToken);

        if (!invoices.Any())
            return new ApiInvoicesContext { ErrorCode = ErrorCode.AccountNotFound };

        return new ApiInvoicesContext
        {
            Organization = organization,
            Invoices = invoices
        };
    }

    public async Task<bool> IsOneTimeInvoiceAlreadyPaidAsync(
        IEnumerable<Invoice> invoices,
        CancellationToken cancellationToken = default)
    {
        var oneTimeInvoices = invoices
            .Where(x => string.Equals(x.Periodicity, "oneTime", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!oneTimeInvoices.Any())
            return false;

        if (oneTimeInvoices.Any(x => x.InvoicePayments.Any(p => p.PaymentStatus == "paid")))
            return true;

        var oneTimeInvoiceIds = oneTimeInvoices.Select(x => x.Id).ToList();

        return await _db.Transactions
            .AsNoTracking()
            .AnyAsync(
                t => t.Invoice != null
                     && oneTimeInvoiceIds.Contains(t.Invoice)
                     && t.TransactionStatus == "success",
                cancellationToken);
    }

    public static string FormatOrganizationPayCodePrefix(string organizationId)
    {
        var raw = (organizationId ?? "").Trim();
        if (raw.Length >= PayCodeOrganizationDigits)
            return raw[..PayCodeOrganizationDigits];

        return raw.PadLeft(PayCodeOrganizationDigits, '0');
    }

    public static string FormatPayCode(string organizationId, long counter)
    {
        if (counter < 1 || counter > 999_999_999)
            throw new InvalidOperationException("Счётчик лицевого счёта вне допустимого диапазона.");

        return FormatOrganizationPayCodePrefix(organizationId) + counter.ToString("D9");
    }

    public static bool TryParsePayCodeCounter(string? payCode, string organizationPrefix, out long counter)
    {
        counter = 0;
        if (string.IsNullOrWhiteSpace(payCode) || payCode.Length != PayCodeTotalLength)
            return false;
        if (!payCode.StartsWith(organizationPrefix, StringComparison.Ordinal))
            return false;

        return long.TryParse(payCode.AsSpan(PayCodeOrganizationDigits, PayCodeCounterDigits), out counter);
    }

    public async Task<string> GenerateNextPayCodeAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var maxCounter = await GetMaxOrganizationPayCodeCounterAsync(organizationId, cancellationToken);
        return FormatPayCode(organizationId, maxCounter + 1);
    }

    private async Task<long> GetMaxOrganizationPayCodeCounterAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var prefix = FormatOrganizationPayCodePrefix(organizationId);
        var existingPayCodes = await _db.Invoices
            .Where(i => i.Client != null
                        && i.ClientNavigation != null
                        && i.ClientNavigation.Organization == organizationId
                        && i.PayCode != null)
            .Select(i => i.PayCode)
            .ToListAsync(cancellationToken);

        long maxCounter = 0;
        foreach (var payCode in existingPayCodes)
        {
            if (TryParsePayCodeCounter(payCode, prefix, out var counter) && counter > maxCounter)
                maxCounter = counter;
        }

        return maxCounter;
    }

    private static JsonCheckResponse CreateCheckErrorResponse(ErrorCode errorCode, string? account = null)
    {
        return new JsonCheckResponse
        {
            Result = (int)errorCode,
            Description = WebApiResponseService.GetErrorDescription(errorCode),
            Account = long.TryParse(account, out var accountLong) ? accountLong : 0L
        };
    }

    private static JsonInvoiceForPaymentItem ToJsonInvoiceForPaymentItem(InvoicePayment payment)
    {
        return new JsonInvoiceForPaymentItem
        {
            InvoiceName = payment.InvoiceNavigation?.NameInvoice ?? string.Empty,
            Period = payment.PeriodValue ?? string.Empty,
            Amount = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(payment.PaymentSumm), CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Создание записи о транзакции. Три комиссии: не переданные сохраняются как 0.
    /// </summary>
    public Transaction CreateTransaction(
        string? invoiceId,
        string? paymentInvId,
        Agent? agent,
        decimal? sum,
        decimal? sumWithFee,
        string txnId,
        string transactionType,
        decimal? lowerCommissionFromOrg = null,
        decimal? upperCommissionFromAgent = null,
        decimal? lowerCommissionToAgent = null,
        string? parentTransactionId = null)
    {
        return new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            Invoice = invoiceId,
            PaymentInvoice = paymentInvId,
            Agent = agent?.Id,
            Summ = sum,
            TransactionSumm = sumWithFee,
            LowerCommissionFromOrg = lowerCommissionFromOrg ?? 0,
            UpperCommissionFromAgent = upperCommissionFromAgent ?? 0,
            LowerCommissionToAgent = lowerCommissionToAgent ?? 0,
            TransactionDate = DateTime.Now,
            TransactionStatus = "success",
            TransactionType = transactionType,
            TxnId = txnId,
            TransactionSystem = "secore",
            ParentTransaction = parentTransactionId
        };
    }

    /// <summary>
    /// Создаёт транзакцию с расчётом всех включённых комиссий (нижняя от организации, верхняя от агента, нижняя к агенту).
    /// </summary>
    public async Task<Transaction> CreateTransactionWithCommissionAsync(
        string? invoiceId,
        string? paymentInvId,
        Agent? agent,
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
            var commissions = await _commissionService.GetCommissionsForTransactionAsync(
                orgId, agent?.Id ?? string.Empty, sum.Value, cancellationToken);
            lowerOrg = commissions.LowerCommissionFromOrg;
            upperAgent = commissions.UpperCommissionFromAgent;
            lowerAgent = commissions.LowerCommissionToAgent;
        }
        return CreateTransaction(
            invoiceId, paymentInvId, agent, sum, sumWithFee, txnId, transactionType,
            lowerOrg, upperAgent, lowerAgent);
    }


    public async Task<ApiPaymentApplyResult> ApplyApiPaymentAsync(
        List<Invoice> invoices,
        Agent agent,
        decimal requestSum,
        string txnId,
        Transaction secoreTrn,
        CancellationToken cancellationToken = default)
    {
        var paidPayments = new List<InvoicePayment>();
        decimal paidSum = 0m;
        decimal balanceAdded = 0m;

        var oldBalance = invoices.First().Balance ?? 0m;
        var rest = requestSum + oldBalance;

        async Task PayPaymentsListAsync(IEnumerable<InvoicePayment> paymentsToPay, bool requireFullAmount = false)
        {
            foreach (var payment in paymentsToPay)
            {
                if (rest <= 0)
                    break;

                var paymentAmount = payment.PaymentSumm ?? 0;
                if (paymentAmount <= 0)
                    continue;

                if (requireFullAmount && rest < paymentAmount)
                    break;

                var toPay = requireFullAmount ? paymentAmount : Math.Min(rest, paymentAmount);
                rest -= toPay;
                payment.PaymentStatus = "paid";

                paidPayments.Add(payment);
                paidSum += toPay;

                var trn = await CreateTransactionWithCommissionAsync(
                    payment.Invoice,
                    payment.Id,
                    agent,
                    toPay,
                    toPay,
                    txnId,
                    "payPaymentInvoice",
                    cancellationToken);
                _db.Transactions.Add(trn);
            }
        }

        var firstInvoice = invoices.First();

        var duePayments = invoices.SelectMany(GetDuePayments).ToList();
        if (duePayments.Any())
        {
            await PayPaymentsListAsync(duePayments.OrderBy(p => p.DateFrom), requireFullAmount: true);
        }

        var planPayments = invoices.SelectMany(GetDuePaymentsbyPlan).ToList();
        if (planPayments.Any() && rest > 0)
        {
            await PayPaymentsListAsync(planPayments.OrderBy(p => p.DateFrom), requireFullAmount: true);
        }

        if (paidPayments.Any())
            secoreTrn.Invoice = paidPayments.First().Invoice;

        if (!string.IsNullOrWhiteSpace(secoreTrn.Invoice) && !string.IsNullOrWhiteSpace(secoreTrn.Id))
        {
            var refreshPayCode = invoices.FirstOrDefault(i => i.Id == secoreTrn.Invoice)?.PayCode
                ?? invoices.FirstOrDefault()?.PayCode;
            var anchorId = secoreTrn.Invoice ?? invoices.FirstOrDefault()?.Id;
            if (!string.IsNullOrWhiteSpace(refreshPayCode) && !string.IsNullOrWhiteSpace(anchorId))
                _invoiceQrService.EnqueueRefreshForPayCode(refreshPayCode, anchorId);
        }

        invoices.ForEach(i => i.Balance = rest);

        var oldPositive = Math.Max(0m, oldBalance);
        var newPositive = Math.Max(0m, rest);
        balanceAdded = Math.Max(0m, newPositive - oldPositive);

        await MarkAppointmentsPaidForInvoicePaymentsAsync(paidPayments, cancellationToken);

        return new ApiPaymentApplyResult
        {
            PaidPayments = paidPayments,
            PaidSum = paidSum,
            BalanceAdded = balanceAdded,
            Rest = rest,
            FirstInvoice = firstInvoice
        };
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
                var serviceSummTyiyn = (orgService.ServiceSumm ?? 0) * 100m;
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
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var organizationId = input.OrganizationId;
        var userId = input.UserId;
        var clients = input.Clients;
        var useManualService = input.UseManualService;
        var orgServices = input.OrgServices;
        var requestPeriodicity = input.Periodicity ?? "monthly";
        var useExistingPayCode = !string.IsNullOrWhiteSpace(input.PayCode);

        long maxCounter = await GetMaxOrganizationPayCodeCounterAsync(organizationId, cancellationToken);

        var createdIds = new List<string>();
        var createdInvoices = new List<Invoice>();
        var ru = CultureInfo.GetCultureInfo("ru-RU");

        Dictionary<string, string>? latestPayCodeByClientId = null;
        if (input.ReuseClientPayCodeWhenExists)
        {
            var clientIds = clients.Select(c => c.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            if (clientIds.Count > 0)
            {
                var payCodeRows = await _db.Invoices
                    .AsNoTracking()
                    .Where(i => i.Client != null &&
                                clientIds.Contains(i.Client) &&
                                i.PayCode != null &&
                                i.PayCode != "")
                    .Select(i => new { i.Client, i.PayCode, i.DateCreated })
                    .ToListAsync(cancellationToken);

                latestPayCodeByClientId = payCodeRows
                    .GroupBy(x => x.Client!)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(x => x.DateCreated).First().PayCode!.Trim());
            }
        }

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
            string payCode;
            var hassameaccount = input.Hassameaccount || useExistingPayCode;
            if (input.ReuseClientPayCodeWhenExists)
            {
                if (latestPayCodeByClientId != null &&
                    latestPayCodeByClientId.TryGetValue(client.Id, out var existingPayCode) &&
                    !string.IsNullOrWhiteSpace(existingPayCode))
                {
                    payCode = existingPayCode;
                }
                else
                {
                    payCode = FormatPayCode(organizationId, ++maxCounter);
                }

                hassameaccount = true;
            }
            else
            {
                payCode = useExistingPayCode ? input.PayCode!.Trim() : FormatPayCode(organizationId, ++maxCounter);
            }

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
                    var serviceSummTyiyn = (orgService.ServiceSumm ?? 0) * 100m;
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
                Hassameaccount = hassameaccount,
                NameInvoice = !string.IsNullOrWhiteSpace(input.NameInvoice) ? input.NameInvoice : "Счет " + (client.ClientName ?? client.Id)
            };
            _db.Invoices.Add(invoice);
            createdInvoices.Add(invoice);

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
        await tx.CommitAsync(cancellationToken);

        foreach (var group in createdInvoices
                     .Where(i => !string.IsNullOrWhiteSpace(i.PayCode))
                     .GroupBy(i => i.PayCode!.Trim()))
        {
            _invoiceQrService.EnqueueRefreshForPayCode(group.Key, group.First().Id);
        }

        return createdIds;
    }

    async Task MarkAppointmentsPaidForInvoicePaymentsAsync(
        IReadOnlyList<InvoicePayment> paidPayments,
        CancellationToken cancellationToken)
    {
        if (paidPayments == null || paidPayments.Count == 0)
            return;

        var invoiceIds = paidPayments
            .Select(p => p.Invoice)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var fromAppointmentInvoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id) && i.FromAppointments)
            .Select(i => new { i.Id, i.Client })
            .ToListAsync(cancellationToken);

        if (fromAppointmentInvoices.Count == 0)
            return;

        var patientByInvoiceId = fromAppointmentInvoices.ToDictionary(x => x.Id, x => x.Client);
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        foreach (var payment in paidPayments)
        {
            if (string.IsNullOrWhiteSpace(payment.Invoice) || !patientByInvoiceId.TryGetValue(payment.Invoice, out var patientId))
                continue;
            if (string.IsNullOrWhiteSpace(patientId) || payment.DateFrom == null)
                continue;

            var startsAt = payment.DateFrom.Value;
            var endsAt = payment.DateTo ?? startsAt;

            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(a =>
                        a.PatientId == patientId &&
                        a.IsActive &&
                        a.StartsAt == startsAt &&
                        a.EndsAt == endsAt,
                    cancellationToken);

            if (appointment == null)
                continue;

            appointment.PaymentType = AppointmentPaymentQrSecorePaid;
            appointment.UpdatedAt = now;
        }
    }

    public async Task<CreateOneTimeInvoiceResult> CreateOneTimeInvoiceAsync(
        CreateOneTimeInvoiceInput input,
        CancellationToken cancellationToken = default)
    {
        var client = await _db.OrganizationClients
            .FirstOrDefaultAsync(c =>
                c.Id == input.ClientId &&
                c.Organization == input.OrganizationId &&
                c.ClientStatus == 1,
                cancellationToken);

        if (client == null)
            throw new InvalidOperationException("Клиент не найден.");

        var invoiceId = Guid.NewGuid().ToString();
        var payCode = string.IsNullOrWhiteSpace(input.PayCode)
            ? await GenerateNextPayCodeAsync(input.OrganizationId, cancellationToken)
            : input.PayCode.Trim();
        var now = ParsersHelper.NowForTimestamp();

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = new Invoice
        {
            Id = invoiceId,
            DateCreated = now,
            UserCreater = input.UserId,
            InvoiceStatus = "actual",
            Periodicity = "oneTime",
            DateStartInvoice = input.DateStartInvoice ?? now,
            DateEndInvoice = input.DateEndInvoice,
            Balance = input.Balance,
            PayCode = payCode,
            Client = client.Id,
            FixedSumm = input.FixedSumm,
            AutoProlongation = false,
            NextStartInvoice = null,
            Hassameaccount = input.Hassameaccount,
            NameInvoice = string.IsNullOrWhiteSpace(input.InvoiceName)
                ? "Разовый платёж"
                : input.InvoiceName.Trim(),
            FromAppointments = input.FromAppointments
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var serviceLine in input.ServiceLines)
        {
            _db.InvoiceServices.Add(new InvoiceService
            {
                Id = Guid.NewGuid().ToString(),
                Invoice = invoiceId,
                Service = serviceLine.OrganizationServiceId,
                ServiceSumm = serviceLine.ServiceSumm
            });
        }

        if (input.CreateInvoicePayment)
        {
            _db.InvoicePayments.Add(new InvoicePayment
            {
                Id = Guid.NewGuid().ToString(),
                Invoice = invoiceId,
                DateFrom = input.PaymentDateFrom,
                DateTo = input.PaymentDateTo,
                PaymentStatus = "non_paid",
                PeriodValue = input.PaymentPeriodValue,
                PaymentSumm = input.PaymentSumm ?? input.FixedSumm
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        if (input.GenerateQrOnCreate)
        {
            var purchaseSom = input.FixedSumm > 0 ? input.FixedSumm / 100m : (decimal?)null;
            await _invoiceQrService.GenerateForInvoiceAsync(invoice, purchaseSom, cancellationToken);
        }
        else if (!input.FromAppointments)
        {
            _invoiceQrService.EnqueueRefreshForPayCode(payCode, invoiceId);
        }

        return new CreateOneTimeInvoiceResult
        {
            InvoiceId = invoiceId,
            PayCode = payCode
        };
    }

    /// <summary>
    /// Обновляет счёт, созданный из записи: услуги, сумму, период платежа и QR SECORE (если не оплачен).
    /// </summary>
    public async Task SyncAppointmentInvoiceFromBookingAsync(
        string organizationId,
        string invoiceId,
        Appointment appointment,
        OrganizationClient patient,
        User? doctor,
        string paymentType,
        DateTime previousStartsAt,
        DateTime previousEndsAt,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
            .Include(i => i.InvoicePayments)
            .Include(i => i.InvoiceServices)
            .FirstOrDefaultAsync(
                i => i.Id == invoiceId && i.FromAppointments,
                cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException("Счёт для записи не найден.");

        var clientOk = await _db.OrganizationClients
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == invoice.Client && c.Organization == organizationId,
                cancellationToken);

        if (!clientOk)
            throw new InvalidOperationException("Клиент не найден.");

        var serviceLines = appointment.AppointmentServices.ToList();
        var totalTyiyn = serviceLines.Sum(x => (x.PriceTyiyn ?? 0) * Math.Max(1, x.Quantity));
        if (totalTyiyn <= 0)
            throw new InvalidOperationException("Сумма услуг должна быть больше 0.");

        var doctorName = string.IsNullOrWhiteSpace(doctor?.Name) ? "Врач" : doctor!.Name!;
        var patientName = string.IsNullOrWhiteSpace(patient.ClientName) ? "Пациент" : patient.ClientName!;
        var titleDate = appointment.StartsAt.ToString("dd.MM.yyyy");
        invoice.NameInvoice = $"Приём {patientName} • {doctorName} • {titleDate}";
        invoice.DateStartInvoice = appointment.StartsAt.Date;
        invoice.DateEndInvoice = appointment.StartsAt.Date;
        invoice.FixedSumm = totalTyiyn;

        foreach (var existing in invoice.InvoiceServices.ToList())
            _db.InvoiceServices.Remove(existing);

        foreach (var line in serviceLines)
        {
            _db.InvoiceServices.Add(new InvoiceService
            {
                Id = Guid.NewGuid().ToString(),
                Invoice = invoice.Id,
                Service = line.OrganizationServiceId,
                ServiceSumm = (line.PriceTyiyn ?? 0) * Math.Max(1, line.Quantity)
            });
        }

        var payment = invoice.InvoicePayments
            .FirstOrDefault(p =>
                p.DateFrom == previousStartsAt &&
                p.DateTo == previousEndsAt)
            ?? invoice.InvoicePayments
                .OrderByDescending(p => p.DateFrom)
                .FirstOrDefault();

        if (payment != null)
        {
            payment.DateFrom = appointment.StartsAt;
            payment.DateTo = appointment.EndsAt;
            payment.PaymentSumm = totalTyiyn;
            payment.PeriodValue = appointment.StartsAt.ToString("dd.MM.yyyy");
        }

        await _db.SaveChangesAsync(cancellationToken);

        var invoicePaid = invoice.InvoicePayments.Any(p =>
            string.Equals(p.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase));

        if (string.Equals(paymentType, "qr_secore", StringComparison.OrdinalIgnoreCase) && !invoicePaid)
        {
            var purchaseSom = totalTyiyn / 100m;
            await _invoiceQrService.GenerateForInvoiceAsync(invoice, purchaseSom, cancellationToken);
        }
    }

    /// <summary>
    /// Транзакции при ручной фиксации оплаты приёма (касса / внешний QR / карта).
    /// По аналогии с JSON API: приход payFromExternal* и разнесение payPaymentInvoice с комиссиями.
    /// Не вызывает SaveChanges.
    /// </summary>
    public async Task ApplyManualAppointmentMarkPaidTransactionsAsync(
        string organizationId,
        Invoice invoice,
        IReadOnlyList<InvoicePayment> payments,
        string? appointmentPaymentType,
        CancellationToken cancellationToken = default)
    {
        if (payments == null || payments.Count == 0)
            return;

        var agent = await ResolveOrganizationPaymentAgentAsync(organizationId, cancellationToken);
        if (ManualMarkPaidRequiresPaymentAgent(appointmentPaymentType) && agent == null)
        {
            throw new InvalidOperationException(
                "Для организации не настроен платёжный агент. Невозможно записать транзакцию оплаты.");
        }

        var paymentsNeedingTrn = new List<InvoicePayment>();
        foreach (var payment in payments)
        {
            var alreadyRecorded = await _db.Transactions.AsNoTracking().AnyAsync(
                t => t.PaymentInvoice == payment.Id && t.TransactionStatus == "success",
                cancellationToken);
            if (!alreadyRecorded)
                paymentsNeedingTrn.Add(payment);
        }

        if (paymentsNeedingTrn.Count == 0)
            return;

        var totalTyiyn = paymentsNeedingTrn.Sum(p => p.PaymentSumm ?? 0);
        if (totalTyiyn <= 0)
            return;

        var incomingType = MapManualAppointmentPaymentToIncomingTransactionType(appointmentPaymentType);
        var txnId = $"appt-manual-{Guid.NewGuid():N}";

        var parentTrn = CreateTransaction(
            invoice.Id,
            null,
            agent,
            totalTyiyn,
            totalTyiyn,
            txnId,
            incomingType);
        _db.Transactions.Add(parentTrn);

        foreach (var payment in paymentsNeedingTrn)
        {
            var paymentSumm = payment.PaymentSumm ?? 0;
            if (paymentSumm <= 0)
                continue;

            var trn = await CreateTransactionWithCommissionAsync(
                payment.Invoice,
                payment.Id,
                agent,
                paymentSumm,
                paymentSumm,
                txnId,
                "payPaymentInvoice",
                cancellationToken);
            _db.Transactions.Add(trn);
        }

        if (!string.IsNullOrWhiteSpace(invoice.PayCode))
            _invoiceQrService.EnqueueRefreshForPayCode(invoice.PayCode.Trim(), invoice.Id);
    }

    async Task<Agent?> ResolveOrganizationPaymentAgentAsync(string organizationId, CancellationToken cancellationToken)
    {
        var link = await _db.AgentCommissions
            .AsNoTracking()
            .Include(ac => ac.Agent)
            .Where(ac => ac.OrganizationId == organizationId && ac.Agent != null)
            .OrderBy(ac => ac.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return link?.Agent;
    }

    static string MapManualAppointmentPaymentToIncomingTransactionType(string? appointmentPaymentType)
    {
        var normalized = (appointmentPaymentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "qr_external" => "payFromExternalQR",
            "card" => "payFromExternalCard",
            "cash" => "payFromCash",
            "unpaid" => "payFromCash",
            _ => "payFromCash"
        };
    }

    static bool ManualMarkPaidRequiresPaymentAgent(string? appointmentPaymentType)
    {
        var normalized = (appointmentPaymentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "qr_secore" or "qr_secore_paid";
    }

    /// <summary>
    /// Возврат оплаты по позиции графика счёта: credit-транзакции с отрицательной суммой и parent_transaction.
    /// Не вызывает SaveChanges.
    /// </summary>
    public async Task RefundInvoicePaymentAsync(InvoicePayment payment, CancellationToken cancellationToken = default)
    {
        if (payment == null || string.IsNullOrWhiteSpace(payment.Id))
            return;

        var paymentTrns = await _db.Transactions
            .Where(t =>
                t.TransactionStatus == "success" &&
                t.PaymentInvoice == payment.Id &&
                t.TransactionType != "credit" &&
                (t.Summ ?? 0) > 0)
            .ToListAsync(cancellationToken);

        var txnIds = paymentTrns
            .Select(t => t.TxnId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var related = await _db.Transactions
            .Where(t =>
                t.TransactionStatus == "success" &&
                t.TransactionType != "credit" &&
                (t.Summ ?? 0) > 0 &&
                (
                    t.PaymentInvoice == payment.Id ||
                    (t.PaymentInvoice == null && t.TxnId != null && txnIds.Contains(t.TxnId))
                ))
            .ToListAsync(cancellationToken);

        foreach (var source in related)
        {
            var alreadyRefunded = await _db.Transactions.AnyAsync(
                t => t.ParentTransaction == source.Id && t.TransactionStatus == "success",
                cancellationToken);
            if (alreadyRefunded)
                continue;

            var negSumm = -(source.Summ ?? 0);
            var negTrnSumm = -(source.TransactionSumm ?? source.Summ ?? 0);
            var refund = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                Invoice = source.Invoice,
                PaymentInvoice = source.PaymentInvoice,
                Agent = source.Agent,
                Summ = negSumm,
                TransactionSumm = negTrnSumm,
                LowerCommissionFromOrg = -(source.LowerCommissionFromOrg ?? 0),
                UpperCommissionFromAgent = -(source.UpperCommissionFromAgent ?? 0),
                LowerCommissionToAgent = -(source.LowerCommissionToAgent ?? 0),
                TransactionDate = DateTime.Now,
                TransactionStatus = "success",
                TransactionType = "credit",
                TxnId = $"refund-{source.Id}-{Guid.NewGuid():N}",
                TransactionSystem = "secore",
                ParentTransaction = source.Id
            };
            _db.Transactions.Add(refund);
        }

        payment.PaymentStatus = "non_paid";

        if (!string.IsNullOrWhiteSpace(payment.Invoice))
        {
            var payCode = await _db.Invoices
                .AsNoTracking()
                .Where(i => i.Id == payment.Invoice)
                .Select(i => i.PayCode)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(payCode))
                _invoiceQrService.EnqueueRefreshForPayCode(payCode.Trim(), payment.Invoice);
        }
    }



    // ------------------------------------------------------------------------
    // СТАРЫЙ КОД , ИСПОЛЬЗУЕТСЯ В WebApiController 


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

}

