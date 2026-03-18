using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;
using System.Net.Http.Headers;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Models.JsonApiModels;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("WebApi/[action]")]
    [ApiController]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class JsonWebApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly WebApiAuthService _authService;
        private readonly OperationsByInvoices _oper;

        public JsonWebApiController(AppDbContext db, WebApiAuthService authService, OperationsByInvoices oper)
        {
            _db = db;
            _authService = authService;
            _oper = oper;
        }

        [HttpPost]
        public async Task<ActionResult<JsonCheckResponse>> check([FromBody] JsonCheckRequest request)
        {
            if (request == null)
                return CreateCheckErrorResponse(ErrorCode.UnknownRequest);

            if (!TryGetBasicCredentials(out var login, out var password))
                return CreateCheckErrorResponse(ErrorCode.AuthenticationFailed, request.Account);

            if (string.IsNullOrWhiteSpace(request.ServiceId))
                return CreateCheckErrorResponse(ErrorCode.ServiceIdNotFound, request.Account);

            if (string.IsNullOrWhiteSpace(request.Account))
                return CreateCheckErrorResponse(ErrorCode.AccountNotProvided);

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Account, @"^\d+$"))
                return CreateCheckErrorResponse(ErrorCode.InvalidAccount, request.Account);

            var agent = await _authService.AuthorizeAsync(login, password);
            if (agent == null)
                return CreateCheckErrorResponse(ErrorCode.AuthenticationFailed);

            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == request.ServiceId);
            if (organization == null)
                return CreateCheckErrorResponse(ErrorCode.ServiceIdNotFound, request.Account);

            // Дублируем поведение XML-коннектора: account должен принадлежать organization
            if (request.Account.Length < 5 || request.Account.Substring(0, 5) != organization.Id)
                return CreateCheckErrorResponse(ErrorCode.AccountNotFound, request.Account);

            var invoices = await _db.Invoices
                .Include(i => i.InvoicePayments)
                .Where(i => i.PayCode == request.Account
                            && i.InvoiceStatus == "actual"
                            && i.ClientNavigation.Organization == organization.Id)
                .ToListAsync();

            if (!invoices.Any())
                return CreateCheckErrorResponse(ErrorCode.AccountNotFound, request.Account);

            var firstInvoice = invoices.First();

            var client = await _db.OrganizationClients
                .FirstOrDefaultAsync(c => c.Id == firstInvoice.Client);

            decimal recommendedSum = 0;
            var items = new List<JsonInvoiceForPaymentItem>();

            var duePayments = invoices.SelectMany(_oper.GetDuePayments).ToList();
            if (duePayments.Any())
            {
                recommendedSum += duePayments.Sum(p => p.PaymentSumm ?? 0);
                items.AddRange(duePayments.Select(p => new JsonInvoiceForPaymentItem
                {
                    InvoiceName = p.InvoiceNavigation?.NameInvoice ?? string.Empty,
                    Period = p.PeriodValue ?? string.Empty,
                    Amount = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(p.PaymentSumm), CultureInfo.InvariantCulture)
                }));
            }

            var planPayments = invoices.SelectMany(_oper.GetDuePaymentsbyPlan).ToList();
            if (planPayments.Any())
            {
                recommendedSum += planPayments.Sum(p => p.PaymentSumm ?? 0);
                items.AddRange(planPayments.Select(p => new JsonInvoiceForPaymentItem
                {
                    InvoiceName = p.InvoiceNavigation?.NameInvoice ?? string.Empty,
                    Period = p.PeriodValue ?? string.Empty,
                    Amount = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(p.PaymentSumm), CultureInfo.InvariantCulture)
                }));
            }

            if (!duePayments.Any() && !planPayments.Any())
            {
                var futurePayments = invoices.SelectMany(_oper.GetDuePaymentsFuture).ToList();
                if (futurePayments.Any())
                {
                    recommendedSum += futurePayments.Sum(p => p.PaymentSumm ?? 0);
                    items.AddRange(futurePayments.Select(p => new JsonInvoiceForPaymentItem
                    {
                        InvoiceName = p.InvoiceNavigation?.NameInvoice ?? string.Empty,
                        Period = p.PeriodValue ?? string.Empty,
                        Amount = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(p.PaymentSumm), CultureInfo.InvariantCulture)
                    }));
                }
            }

            var currentBalance = invoices.FirstOrDefault()?.Balance ?? 0m;
            if (currentBalance > 0)
            {
                recommendedSum -= currentBalance;
                if (recommendedSum < 0)
                    recommendedSum = 0;
            }

            return new JsonCheckResponse
            {
                Result = (int)ErrorCode.Success,
                Description = WebApiResponseService.GetErrorDescription(ErrorCode.Success),
                Account = long.TryParse(request.Account, out var accountLong) ? accountLong : 0L,
                BalanceSum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(currentBalance), CultureInfo.InvariantCulture),
                RecomendedPaySum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(recommendedSum), CultureInfo.InvariantCulture),
                Organization = organization.Name ?? string.Empty,
                Subscriber = client?.ClientName ?? string.Empty,
                InvoicesForPayment = items
            };
        }

        [HttpPost]
        public async Task<ActionResult<JsonPayResponse>> pay([FromBody] JsonPayRequest request)
        {
            if (request == null)
                return CreatePayErrorResponse(ErrorCode.UnknownRequest);

            if (!TryGetBasicCredentials(out var login, out var password))
                return CreatePayErrorResponse(ErrorCode.AuthenticationFailed);

            if (string.IsNullOrWhiteSpace(request.ServiceId))
                return CreatePayErrorResponse(ErrorCode.ServiceIdNotFound);

            if (string.IsNullOrWhiteSpace(request.TxnId))
                return CreatePayErrorResponse(ErrorCode.TxnIdNotProvided);

            if (string.IsNullOrWhiteSpace(request.TxnDate) ||
                !DateTime.TryParseExact(request.TxnDate, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out _))
            {
                return CreatePayErrorResponse(ErrorCode.InvalidTxnDate);
            }

            if (string.IsNullOrWhiteSpace(request.Account))
                return CreatePayErrorResponse(ErrorCode.AccountNotProvided);

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Account, @"^\d+$"))
                return CreatePayErrorResponse(ErrorCode.InvalidAccount);

            if (request.PaySum <= 0)
                return CreatePayErrorResponse(ErrorCode.SumMustBeGreaterThanZero);
            var requestSum = request.PaySum * 100m;

            var agent = await _authService.AuthorizeAsync(login, password);
            if (agent == null)
                return CreatePayErrorResponse(ErrorCode.AuthenticationFailed);

            await using var dbTransaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == request.ServiceId);
                if (organization == null)
                    return CreatePayErrorResponse(ErrorCode.ServiceIdNotFound);

                if (request.Account.Length < 5 || request.Account.Substring(0, 5) != organization.Id)
                    return CreatePayErrorResponse(ErrorCode.AccountNotFound);

                var invoices = await _db.Invoices
                    .Include(i => i.InvoicePayments)
                    .Where(i => i.PayCode == request.Account &&
                                i.InvoiceStatus == "actual" &&
                                i.ClientNavigation.Organization == organization.Id)
                    .ToListAsync();

                if (!invoices.Any())
                    return CreatePayErrorResponse(ErrorCode.AccountNotFound);

                var duplicateTxn = await _db.Transactions
                    .AnyAsync(t => t.TxnId == request.TxnId && t.Agent == agent.Id);

                if (duplicateTxn)
                    return CreatePayErrorResponse(ErrorCode.DuplicateCancelledTxnId);

                var paidPayments = new List<InvoicePayment>();
                decimal paidSum = 0m;
                decimal balanceAdded = 0m;

                var oldBalance = invoices.First().Balance ?? 0m;
                // Всегда учитываем накопленный баланс (включая положительный),
                // чтобы следующий платеж мог погасить долг за счет balance + paySum.
                // Если баланс отрицательный — это уменьшает доступную сумму (долг).
                var rest = requestSum + oldBalance;

                var secoreTrn = _oper.CreateTransaction(null, null, agent, requestSum, requestSum, request.TxnId, "payFromAPI");
                // Заполняем, по какому инвойсу пришла оплата (для аналитики/поиска).
                // По умолчанию (пополнение баланса) — первый актуальный инвойс аккаунта.
                secoreTrn.Invoice = invoices.First().Id;
                _db.Transactions.Add(secoreTrn);

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

                        var trn = await _oper.CreateTransactionWithCommissionAsync(
                            payment.Invoice,
                            payment.Id,
                            agent,
                            toPay,
                            toPay,
                            request.TxnId,
                            "payPaymentInvoice");
                        _db.Transactions.Add(trn);
                    }
                }

                var firstInvoice = invoices.First();

                var duePayments = invoices.SelectMany(_oper.GetDuePayments).ToList();
                if (duePayments.Any())
                {
                    await PayPaymentsListAsync(duePayments.OrderBy(p => p.DateFrom), requireFullAmount: true);
                }

                var planPayments = invoices.SelectMany(_oper.GetDuePaymentsbyPlan).ToList();
                if (planPayments.Any() && rest > 0)
                {
                    await PayPaymentsListAsync(planPayments.OrderBy(p => p.DateFrom), requireFullAmount: true);
                }

                // Если что-то погасили — привяжем платеж к инвойсу первого погашенного платежа.
                if (paidPayments.Any())
                    secoreTrn.Invoice = paidPayments.First().Invoice;

                // Всегда фиксируем новый баланс (может быть >0, =0 или <0)
                invoices.ForEach(i => i.Balance = rest);
                // balanceAdded: сколько "прибавилось" на положительный баланс (не может быть отрицательным)
                var oldPositive = Math.Max(0m, oldBalance);
                var newPositive = Math.Max(0m, rest);
                balanceAdded = Math.Max(0m, newPositive - oldPositive);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                var paidInvoicesText = paidPayments.Any()
                    ? string.Join("\n",
                        paidPayments.Select(p =>
                            $"Инвойс: {p.InvoiceNavigation?.NameInvoice}, " +
                            $"Период: {p.PeriodValue ?? "не указан"}, " +
                            $"Сумма: {ParsersHelper.ToMoneyStringFromCents(p.PaymentSumm)} KGS (оплачено)"))
                    : string.Empty;

                return new JsonPayResponse
                {
                    Account = request.Account,
                    Result = (int)ErrorCode.Success,
                    Description = BuildPaySuccessDescription(paidSum, rest),
                    SecoreTxnId = secoreTrn.Id,
                    TxnId = request.TxnId,
                    TxnDate = request.TxnDate,
                    TransactionDateTime = secoreTrn.TransactionDate?.ToString("yyyyMMddHHmmss") ?? string.Empty,
                    BalanceSum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(firstInvoice.Balance), CultureInfo.InvariantCulture),
                    PaidInvoices = paidInvoicesText,
                    PaidSum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(paidSum), CultureInfo.InvariantCulture),
                    BalanceAdded = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(balanceAdded), CultureInfo.InvariantCulture)
                };
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                return CreatePayErrorResponse(ErrorCode.UnknownRequest);
            }
        }

        private static string BuildPaySuccessDescription(decimal paidSum, decimal restBalance)
        {
            // restBalance < 0: долг еще остался (не хватает денег)
            if (restBalance < 0)
                return $"Платеж успешно принят. Не хватает {ParsersHelper.ToMoneyStringFromCents(Math.Abs(restBalance))} для погашения долга.";

            // paidSum > 0: что-то погасили по графику/долгам
            if (paidSum > 0)
                return "Платеж успешно принят. Долг погашен/списание выполнено.";

            // иначе просто пополнили баланс
            return "Платеж успешно принят. Баланс пополнен.";
        }

        [HttpPost]
        public async Task<ActionResult<JsonPaymentInfoResponse>> payInfo([FromBody] JsonPaymentInfoRequest request)
        {
            if (request == null)
                return CreatePayInfoErrorResponse(ErrorCode.UnknownRequest);

            if (!TryGetBasicCredentials(out var login, out var password))
                return CreatePayInfoErrorResponse(ErrorCode.AuthenticationFailed);

            if (string.IsNullOrWhiteSpace(request.TxnId))
                return CreatePayInfoErrorResponse(ErrorCode.TxnIdNotProvided);

            var agent = await _authService.AuthorizeAsync(login, password);
            if (agent == null)
                return CreatePayInfoErrorResponse(ErrorCode.AuthenticationFailed);

            var transaction = await _db.Transactions
                .FirstOrDefaultAsync(t =>
                    t.TxnId == request.TxnId &&
                    t.Agent == agent.Id &&
                    t.TransactionSystem == "secore");

            // Если оплата не найдена — paymentStatus = 3
            if (transaction == null)
            {
                return new JsonPaymentInfoResponse
                {
                    Result = (int)ErrorCode.Success,
                    Description = "Оплата не найдена.",
                    SecoreTxnId = string.Empty,
                    TxnId = request.TxnId,
                    PaymentStatus = "3",
                    TransactionDateTime = string.Empty
                };
            }

            var paymentStatus = transaction.TransactionStatus == "success" ? "1" : "0";
            var description = transaction.TransactionStatus == "success"
                ? "Платеж успешно принят."
                : "Платеж не принят.";

            return new JsonPaymentInfoResponse
            {
                Result = (int)ErrorCode.Success,
                Description = description,
                SecoreTxnId = transaction.Id,
                TxnId = request.TxnId,
                PaymentStatus = paymentStatus,
                TransactionDateTime = transaction.TransactionDate?.ToString("yyyyMMddHHmmss") ?? string.Empty
            };
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

        private static JsonPayResponse CreatePayErrorResponse(ErrorCode errorCode)
        {
            return new JsonPayResponse
            {
                Result = (int)errorCode,
                Description = WebApiResponseService.GetErrorDescription(errorCode)
            };
        }

        private static JsonPaymentInfoResponse CreatePayInfoErrorResponse(ErrorCode errorCode)
        {
            return new JsonPaymentInfoResponse
            {
                Result = (int)errorCode,
                Description = WebApiResponseService.GetErrorDescription(errorCode)
            };
        }

        private bool TryGetBasicCredentials(out string login, out string password)
        {
            login = string.Empty;
            password = string.Empty;

            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
                return false;

            if (!AuthenticationHeaderValue.TryParse(authHeaderValues.FirstOrDefault(), out var headerValue))
                return false;

            if (!string.Equals(headerValue.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(headerValue.Parameter))
                return false;

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue.Parameter));
            }
            catch
            {
                return false;
            }

            var idx = decoded.IndexOf(':');
            if (idx <= 0)
                return false;

            login = decoded.Substring(0, idx);
            password = decoded.Substring(idx + 1);
            return !(string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password));
        }

        
    }
}

