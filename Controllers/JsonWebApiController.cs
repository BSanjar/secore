using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
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

            return await _oper.CreateJsonCheckResponseAsync(request.ServiceId, request.Account);
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
                var context = await _oper.GetApiInvoicesContextAsync(request.ServiceId, request.Account);
                if (context.ErrorCode == ErrorCode.ServiceIdNotFound)
                    return CreatePayErrorResponse(ErrorCode.ServiceIdNotFound);

                if (context.ErrorCode == ErrorCode.AccountNotFound)
                    return CreatePayErrorResponse(ErrorCode.AccountNotFound);

                var invoices = context.Invoices;

                var duplicateTxn = await _db.Transactions
                    .AnyAsync(t => t.TxnId == request.TxnId && t.Agent == agent.Id);

                if (duplicateTxn)
                    return CreatePayErrorResponse(ErrorCode.DuplicateCancelledTxnId);

                var oneTimeAlreadyPaid = await _oper.IsOneTimeInvoiceAlreadyPaidAsync(invoices);
                if (oneTimeAlreadyPaid)
                    return CreatePayErrorResponse(ErrorCode.InvoiceAlreadyPaid);

                var secoreTrn = _oper.CreateTransaction(null, null, agent, requestSum, requestSum, request.TxnId, "payFromAPI");
                // Заполняем, по какому инвойсу пришла оплата (для аналитики/поиска).
                // По умолчанию (пополнение баланса) - первый актуальный инвойс аккаунта.
                secoreTrn.Invoice = invoices.First().Id;
                _db.Transactions.Add(secoreTrn);

                var paymentResult = await _oper.ApplyApiPaymentAsync(invoices, agent, requestSum, request.TxnId, secoreTrn);

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                var paidInvoicesText = _oper.BuildPaidInvoicesText(paymentResult.PaidPayments);

                return new JsonPayResponse
                {
                    Account = request.Account,
                    Result = (int)ErrorCode.Success,
                    Description = _oper.BuildPaySuccessDescription(paymentResult.PaidSum, paymentResult.Rest),
                    SecoreTxnId = secoreTrn.Id,
                    TxnId = request.TxnId,
                    TxnDate = request.TxnDate,
                    TransactionDateTime = secoreTrn.TransactionDate?.ToString("yyyyMMddHHmmss") ?? string.Empty,
                    BalanceSum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(paymentResult.FirstInvoice.Balance), CultureInfo.InvariantCulture),
                    PaidInvoices = paidInvoicesText,
                    PaidSum = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(paymentResult.PaidSum), CultureInfo.InvariantCulture),
                    BalanceAdded = decimal.Parse(ParsersHelper.ToMoneyStringFromCents(paymentResult.BalanceAdded), CultureInfo.InvariantCulture)
                };
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                return CreatePayErrorResponse(ErrorCode.UnknownRequest);
            }
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
            // Если оплата не найдена - paymentStatus = 3
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
