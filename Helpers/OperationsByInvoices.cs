using WebApplication1.Models.DBModels;
using WebApplication1.Services;

namespace WebApplication1.Helpers
{
    public class OperationsByInvoices
    {
        private readonly AppDbContext _db;

        public OperationsByInvoices(AppDbContext db)
        {
            _db = db;
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
                .ThenBy(p => p.PaymentSumm);
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
                .ThenBy(p => p.PaymentSumm);
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
                .ThenBy(p => p.PaymentSumm);
        }


        public string BuildInvoicesText(IEnumerable<InvoicePayment> payments)
        {
            return string.Join("; ",
                payments
                    .GroupBy(p => p.InvoiceNavigation)
                    .Select(g =>
                    {
                        var total = g.Sum(p => p.PaymentSumm);

                        return $"{g.Key.NameInvoice}: " +
                               $"за {string.Join(", ", g.Select(p => p.PeriodValue))}: " +
                               $"Сумма(KGS): {ParsersHelper.ToMoneyStringFromCents(total)}";
                    }));
        }

        /// <summary>
        /// Создание записи о транзакции (единое место — меньше риска ошибок)
        /// </summary>
        public Models.DBModels.Transaction CreateTransaction(
            string invoiceId,
            string paymentInvId,
            Agent agent,
            decimal? sum,
            decimal? sumWithFee,
            string txnId,
            string transactionType)
        {
            return new Models.DBModels.Transaction
            {
                Id = Guid.NewGuid().ToString(),
                Invoice = invoiceId,
                PaymentInvoice = paymentInvId,
                Agent = agent.Id,
                Summ = sum,
                TransactionSumm = sumWithFee,
                TransactionDate = DateTime.Now,
                TransactionStatus = "success",
                TransactionType = transactionType,
                TxnId = txnId,
                TransactionSystem = "secore"
            };
        }

        /// <summary>
        /// Погашение платежей в рамках одного или нескольких инвойсов
        /// Возвращает остаток средств
        /// </summary>
        public decimal? PayPayments(
            IEnumerable<InvoicePayment> payments,
            decimal? amount,
            Agent agent,
            string txnId)
        {
            foreach (var payment in payments)
            {
                // если остатка не хватает — прекращаем
                if (amount < payment.PaymentSumm)
                    break;

                // уменьшаем остаток
                amount -= payment.PaymentSumm;

                // помечаем платеж как оплаченный
                payment.PaymentStatus = "paid";

                // добавляем запись о транзакции
                _db.Transactions.Add(
                    CreateTransaction(payment.Invoice,payment.Id, agent, payment.PaymentSumm,payment.PaymentSumm, txnId, "payPaymentInvoice"));
            }

            return amount;
        }
    }
}
