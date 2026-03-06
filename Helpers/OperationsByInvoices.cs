using WebApplication1.Models.DBModels;
using WebApplication1.Services;
using WebApplication1.Helpers;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Helpers
{
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
        public Models.DBModels.Transaction CreateTransaction(
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
            return new Models.DBModels.Transaction
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
        public async Task<Models.DBModels.Transaction> CreateTransactionWithCommissionAsync(
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
    }
}
