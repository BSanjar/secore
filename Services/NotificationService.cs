using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;

namespace WebApplication1.Services
{
    /// <summary>
    /// Сервис для создания уведомлений в БД
    /// Различные части системы (монолит, job, внешние API) используют этот сервис для создания уведомлений
    /// </summary>
    public class NotificationService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext db,
            ILogger<NotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Создаёт уведомление о просроченном платеже
        /// </summary>
        public async Task CreateOverduePaymentNotificationAsync(
            OrganizationClient client, 
            Invoice invoice, 
            InvoicePayment payment,
            decimal requiredAmount)
        {
            var clientName = client.ClientName ?? "Клиент";
            var invoiceName = invoice.NameInvoice ?? "Счёт";
            var amountSom = requiredAmount / 100m; // Конвертация из тыйынов в сомы

            var subject = "Просроченный платёж";
            var message = $@"
<h2>Уважаемый(ая) {clientName}!</h2>
<p>У вас просрочен платёж по счёту <strong>{invoiceName}</strong>.</p>
<p><strong>Сумма к оплате:</strong> {amountSom:N2} сом</p>
<p><strong>Период:</strong> {payment.DateFrom:dd.MM.yyyy} - {payment.DateTo:dd.MM.yyyy}</p>
<p>Пожалуйста, пополните баланс для автоматического списания или произведите оплату вручную.</p>
<p>PayCode: <strong>{invoice.PayCode}</strong></p>
";

            await CreateNotificationsForClientAsync(client, subject, message);
        }

        /// <summary>
        /// Создаёт уведомление о недостаточном балансе
        /// </summary>
        public async Task CreateInsufficientBalanceNotificationAsync(
            OrganizationClient client, 
            Invoice invoice, 
            InvoicePayment payment,
            decimal requiredAmount,
            decimal currentBalance)
        {
            var clientName = client.ClientName ?? "Клиент";
            var invoiceName = invoice.NameInvoice ?? "Счёт";
            var amountSom = requiredAmount / 100m;
            var balanceSom = currentBalance / 100m;
            var deficitSom = (requiredAmount - currentBalance) / 100m;

            var subject = "Недостаточно средств для автоплатежа";
            var message = $@"
<h2>Уважаемый(ая) {clientName}!</h2>
<p>Недостаточно средств на балансе для автоматической оплаты счёта <strong>{invoiceName}</strong>.</p>
<p><strong>Требуется:</strong> {amountSom:N2} сом</p>
<p><strong>Текущий баланс:</strong> {balanceSom:N2} сом</p>
<p><strong>Недостаёт:</strong> {deficitSom:N2} сом</p>
<p><strong>Период:</strong> {payment.DateFrom:dd.MM.yyyy} - {payment.DateTo:dd.MM.yyyy}</p>
<p>Пожалуйста, пополните баланс для автоматического списания.</p>
<p>PayCode: <strong>{invoice.PayCode}</strong></p>
";

            await CreateNotificationsForClientAsync(client, subject, message);
        }

        /// <summary>
        /// Создаёт уведомление об успешном автоплатеже
        /// </summary>
        public async Task CreateAutoPaymentSuccessNotificationAsync(
            OrganizationClient client, 
            Invoice invoice, 
            InvoicePayment payment,
            decimal paidAmount)
        {
            var clientName = client.ClientName ?? "Клиент";
            var invoiceName = invoice.NameInvoice ?? "Счёт";
            var amountSom = paidAmount / 100m;

            var subject = "Автоматический платёж выполнен";
            var message = $@"
<h2>Уважаемый(ая) {clientName}!</h2>
<p>Автоматический платёж по счёту <strong>{invoiceName}</strong> успешно выполнен.</p>
<p><strong>Сумма:</strong> {amountSom:N2} сом</p>
<p><strong>Период:</strong> {payment.DateFrom:dd.MM.yyyy} - {payment.DateTo:dd.MM.yyyy}</p>
<p>Спасибо за использование наших услуг!</p>
";

            await CreateNotificationsForClientAsync(client, subject, message);
        }

        /// <summary>
        /// Создаёт уведомления для клиента через все доступные каналы
        /// </summary>
        private async Task CreateNotificationsForClientAsync(
            OrganizationClient client, 
            string subject, 
            string message)
        {
            var notifications = new List<Notification>();

            // Email
            if (!string.IsNullOrWhiteSpace(client.ClientEmail))
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = client.Id,
                    Channel = "email",
                    ContactInfo = client.ClientEmail,
                    Subject = subject,
                    Message = message,
                    Status = "new",
                    CreatedAt = DateHelper.NowForTimestamp()
                });
            }

            // Telegram (если телефон начинается с @)
            if (!string.IsNullOrWhiteSpace(client.ClientPhone) && client.ClientPhone.StartsWith("@"))
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = client.Id,
                    Channel = "telegram",
                    ContactInfo = client.ClientPhone,
                    Subject = subject,
                    Message = message,
                    Status = "new",
                    CreatedAt = DateHelper.NowForTimestamp()
                });
            }

            // WhatsApp (если телефон начинается с +)
            if (!string.IsNullOrWhiteSpace(client.ClientPhone) && client.ClientPhone.StartsWith("+"))
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid().ToString(),
                    ClientId = client.Id,
                    Channel = "whatsapp",
                    ContactInfo = client.ClientPhone,
                    Subject = subject,
                    Message = message,
                    Status = "new",
                    CreatedAt = DateHelper.NowForTimestamp()
                });
            }

            if (notifications.Any())
            {
                _db.Notifications.AddRange(notifications);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Создано {Count} уведомлений для клиента {ClientId}",
                    notifications.Count,
                    client.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Не удалось создать уведомления для клиента {ClientId}. Нет доступных каналов связи.",
                    client.Id);
            }
        }

        /// <summary>
        /// Создаёт уведомление напрямую (для использования из внешних API)
        /// </summary>
        public async Task<Notification> CreateNotificationAsync(
            string? clientId,
            string channel,
            string contactInfo,
            string? subject,
            string message,
            string? metadata = null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = clientId,
                Channel = channel.ToLower(),
                ContactInfo = contactInfo,
                Subject = subject,
                Message = message,
                Status = "new",
                CreatedAt = DateHelper.NowForTimestamp(),
                RetryCount = 0,
                Metadata = metadata
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Создано уведомление {NotificationId} для канала {Channel}",
                notification.Id,
                channel);

            return notification;
        }
    }
}
