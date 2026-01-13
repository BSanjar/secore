using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Services
{
    /// <summary>
    /// Сервис для обработки автоматических платежей по инвойсам
    /// </summary>
    public class InvoicePaymentService
    {
        private readonly AppDbContext _db;
        private readonly NotificationService _notificationService;
        private readonly ILogger<InvoicePaymentService> _logger;

        public InvoicePaymentService(
            AppDbContext db,
            NotificationService notificationService,
            ILogger<InvoicePaymentService> logger)
        {
            _db = db;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Обрабатывает все просроченные и подошедшие платежи
        /// </summary>
        public async Task ProcessScheduledPaymentsAsync()
        {
            var now = ParsersHelper.NowForTimestamp();
            
            // Находим все неоплаченные платежи, которые должны быть оплачены (срок подошёл или просрочен)
            var duePayments = await _db.InvoicePayments
                .Include(ip => ip.InvoiceNavigation)
                    .ThenInclude(i => i!.ClientNavigation)
                .Where(ip => ip.PaymentStatus == "non_paid" && 
                            ip.DateTo.HasValue && 
                            ip.DateTo.Value <= now &&
                            ip.InvoiceNavigation != null &&
                            ip.InvoiceNavigation.InvoiceStatus == "actual" &&
                            ip.InvoiceNavigation.ClientNavigation != null)
                .ToListAsync();

            _logger.LogInformation("Найдено {Count} платежей для обработки", duePayments.Count);

            foreach (var payment in duePayments)
            {
                try
                {
                    await ProcessPaymentAsync(payment);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке платежа {PaymentId}", payment.Id);
                }
            }
        }

        /// <summary>
        /// Обрабатывает один платёж
        /// </summary>
        private async Task ProcessPaymentAsync(InvoicePayment payment)
        {
            var invoice = payment.InvoiceNavigation!;
            var client = invoice.ClientNavigation!;

            // Определяем сумму платежа
            decimal requiredAmount;
            if (invoice.FixedSumm.HasValue && invoice.FixedSumm.Value > 0)
            {
                // Фиксированная сумма в сомах, конвертируем в тыйыны
                requiredAmount = decimal.Round(invoice.FixedSumm.Value * 100m, 0, MidpointRounding.AwayFromZero);
            }
            else if (invoice.Balance.HasValue && invoice.Balance.Value < 0)
            {
                // Если есть долг, оплачиваем его
                requiredAmount = decimal.Round(Math.Abs(invoice.Balance.Value), 0, MidpointRounding.AwayFromZero);
            }
            else if (!string.IsNullOrWhiteSpace(payment.PaymentSumm) && 
                     decimal.TryParse(payment.PaymentSumm, out var paymentSumm) && 
                     paymentSumm > 0)
            {
                // Сумма из записи платежа (в тыйынах)
                requiredAmount = decimal.Round(paymentSumm, 0, MidpointRounding.AwayFromZero);
            }
            else
            {
                _logger.LogWarning("Не удалось определить сумму платежа для {PaymentId}", payment.Id);
                return;
            }

            var clientBalance = client.ClientBalance ?? 0m;
            var now = ParsersHelper.NowForTimestamp();
            var isOverdue = payment.DateTo.HasValue && payment.DateTo.Value < now;

            // Проверяем баланс клиента
            if (clientBalance >= requiredAmount)
            {
                // Достаточно средств - выполняем автоплатёж
                await ExecuteAutoPaymentAsync(payment, invoice, client, requiredAmount);
            }
            else
            {
                // Недостаточно средств или просрочка - создаём уведомления в БД
                if (isOverdue)
                {
                    await _notificationService.CreateOverduePaymentNotificationAsync(
                        client, invoice, payment, requiredAmount);
                }
                else
                {
                    await _notificationService.CreateInsufficientBalanceNotificationAsync(
                        client, invoice, payment, requiredAmount, clientBalance);
                }
            }
        }

        /// <summary>
        /// Выполняет автоматический платёж
        /// </summary>
        private async Task ExecuteAutoPaymentAsync(
            InvoicePayment payment, 
            Invoice invoice, 
            OrganizationClient client, 
            decimal amount)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. Списываем с баланса клиента (credit - расход)
                var clientCreditTransaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionDate = ParsersHelper.NowForTimestamp(),
                    TransactionStatus = "success",
                    Summ = amount,
                    TransactionSumm = amount,
                    Invoice = invoice.Id,
                    TransactionType = "credit"
                };
                _db.Transactions.Add(clientCreditTransaction);

                // Обновляем баланс клиента
                client.ClientBalance = (client.ClientBalance ?? 0m) - amount;

                // 2. Зачисляем на баланс инвойса (debit - приход)
                var invoiceDebitTransaction = new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionDate = ParsersHelper.NowForTimestamp(),
                    TransactionStatus = "success",
                    Summ = amount,
                    TransactionSumm = amount,
                    Invoice = invoice.Id,
                    TransactionType = "debit"
                };
                _db.Transactions.Add(invoiceDebitTransaction);

                // Обновляем баланс инвойса
                invoice.Balance = (invoice.Balance ?? 0m) + amount;

                // 3. Помечаем платёж как оплаченный
                payment.PaymentStatus = "paid";

                // 4. Если включена автопролонгация, создаём следующий период
                if (invoice.AutoProlongation == true && payment.DateTo.HasValue)
                {
                    await CreateNextPaymentPeriodAsync(invoice, payment.DateTo.Value);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Автоплатёж выполнен: PaymentId={PaymentId}, InvoiceId={InvoiceId}, Amount={Amount}", 
                    payment.Id, invoice.Id, amount);

                // Создаём уведомление об успешном платеже (после коммита транзакции)
                // Если создание уведомления упадёт, платеж уже зафиксирован - это допустимо
                try
                {
                    await _notificationService.CreateAutoPaymentSuccessNotificationAsync(
                        client, invoice, payment, amount);
                }
                catch (Exception notifyEx)
                {
                    // Логируем ошибку, но не прерываем выполнение, так как платеж уже выполнен
                    _logger.LogWarning(notifyEx, 
                        "Ошибка при создании уведомления об успешном платеже {PaymentId}. Платеж выполнен.", 
                        payment.Id);
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Ошибка при выполнении автоплатежа {PaymentId}", payment.Id);
                throw;
            }
        }

        /// <summary>
        /// Создаёт следующий период платежа при автопролонгации
        /// </summary>
        private async Task CreateNextPaymentPeriodAsync(Invoice invoice, DateTime previousPeriodEnd)
        {
            if (string.IsNullOrWhiteSpace(invoice.Periodicity) || 
                invoice.Periodicity == "oneTime" || 
                invoice.Periodicity == "any")
            {
                return;
            }

            var nextDateFrom = previousPeriodEnd.AddDays(1);
            DateTime nextDateTo;

            switch (invoice.Periodicity.ToLower())
            {
                case "daily":
                    nextDateTo = nextDateFrom;
                    break;
                case "weekly":
                    nextDateTo = nextDateFrom.AddDays(6);
                    break;
                case "monthly":
                    nextDateTo = nextDateFrom.AddMonths(1).AddDays(-1);
                    break;
                case "yearly":
                    nextDateTo = nextDateFrom.AddYears(1).AddDays(-1);
                    break;
                default:
                    // Число дней
                    if (int.TryParse(invoice.Periodicity, out var days))
                    {
                        nextDateTo = nextDateFrom.AddDays(days - 1);
                    }
                    else
                    {
                        return;
                    }
                    break;
            }

            var nextPayment = new InvoicePayment
            {
                Id = Guid.NewGuid().ToString(),
                Invoice = invoice.Id,
                DateFrom = nextDateFrom,
                DateTo = nextDateTo,
                PaymentStatus = "non_paid",
                PaymentSumm = invoice.FixedSumm.HasValue ? invoice.FixedSumm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null
            };

            _db.InvoicePayments.Add(nextPayment);
            invoice.NextStartInvoice = nextDateFrom;

            _logger.LogInformation(
                "Создан следующий период платежа для инвойса {InvoiceId}: {DateFrom} - {DateTo}", 
                invoice.Id, nextDateFrom, nextDateTo);
        }
    }
}

