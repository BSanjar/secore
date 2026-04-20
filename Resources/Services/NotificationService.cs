using WebApplication1.Models.DBModels;
using WebApplication1.Helpers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

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

        public async Task<NotificationBatchCreateResult> CreateManualNotificationsAsync(
            IEnumerable<OrganizationClient> clients,
            IEnumerable<string> channels,
            string subject,
            string message,
            string? createdBy,
            CancellationToken cancellationToken = default)
        {
            var result = new NotificationBatchCreateResult();
            var selectedChannels = new HashSet<string>(
                channels
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToLowerInvariant()));

            var recipients = clients
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            result.SelectedClients = recipients.Count;

            if (recipients.Count == 0 || selectedChannels.Count == 0)
            {
                return result;
            }

            var createdAt = ParsersHelper.NowForTimestamp();
            var notifications = new List<Notification>();

            foreach (var client in recipients)
            {
                var queuedForClient = false;

                foreach (var channel in selectedChannels)
                {
                    var contactInfo = ResolveContactInfo(client, channel);
                    if (string.IsNullOrWhiteSpace(contactInfo))
                    {
                        continue;
                    }

                    notifications.Add(CreateQueuedNotification(
                        client.Id,
                        channel,
                        contactInfo,
                        subject,
                        message,
                        BuildManualNotificationMetadata(createdBy, channel, createdAt)));

                    queuedForClient = true;
                    result.CreatedNotifications++;

                    if (channel == "email")
                    {
                        result.EmailNotifications++;
                    }
                    else if (channel == "telegram")
                    {
                        result.TelegramNotifications++;
                    }
                    else if (channel == "whatsapp")
                    {
                        result.WhatsAppNotifications++;
                    }
                }

                if (queuedForClient)
                {
                    result.ClientsQueued++;
                }
                else
                {
                    result.ClientsSkippedWithoutChannel++;
                }
            }

            if (notifications.Count > 0)
            {
                _db.Notifications.AddRange(notifications);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return result;
        }

        public async Task CreateOrUpdateAppointmentReminderAsync(
            Appointment appointment,
            OrganizationClient client,
            User? doctor,
            CancellationToken cancellationToken = default)
        {
            await RemovePendingAppointmentReminderNotificationsAsync(appointment.Id, cancellationToken);

            if (!appointment.IsActive)
            {
                return;
            }

            var plannedCreatedAt = appointment.StartsAt.AddDays(-1);
            var createdAt = plannedCreatedAt > ParsersHelper.NowForTimestamp()
                ? plannedCreatedAt
                : ParsersHelper.NowForTimestamp();

            var clientName = string.IsNullOrWhiteSpace(client.ClientName) ? "Пациент" : client.ClientName.Trim();
            var doctorName = string.IsNullOrWhiteSpace(doctor?.Name) ? "врач" : doctor!.Name!.Trim();
            var appointmentDate = appointment.StartsAt.ToString("dd.MM.yyyy");
            var appointmentTime = appointment.StartsAt.ToString("HH:mm");
            var subject = "Напоминание о приёме";
            var message = $@"
<h2>Здравствуйте, {clientName}!</h2>
<p>Напоминаем, что вы записаны на приём.</p>
<p><strong>Дата:</strong> {appointmentDate}</p>
<p><strong>Время:</strong> {appointmentTime}</p>
<p><strong>Специалист:</strong> {doctorName}</p>
<p>Если планы изменились, пожалуйста, свяжитесь с клиникой заранее.</p>
";

            var notifications = new List<Notification>();
            foreach (var channel in new[] { "email", "telegram", "whatsapp" })
            {
                var contactInfo = ResolveContactInfo(client, channel);
                if (string.IsNullOrWhiteSpace(contactInfo))
                {
                    continue;
                }

                notifications.Add(CreateQueuedNotification(
                    client.Id,
                    channel,
                    contactInfo,
                    subject,
                    message,
                    BuildAppointmentReminderMetadata(appointment, client, doctor, createdAt),
                    createdAt));
            }

            if (notifications.Count == 0)
            {
                _logger.LogWarning(
                    "Не удалось создать напоминание о приёме {AppointmentId}: у клиента {ClientId} нет доступных каналов связи.",
                    appointment.Id,
                    client.Id);
                return;
            }

            _db.Notifications.AddRange(notifications);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Создано {Count} напоминаний о приёме {AppointmentId} для клиента {ClientId} с датой активации {CreatedAt}.",
                notifications.Count,
                appointment.Id,
                client.Id,
                createdAt);
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
            var email = client.ClientEmail?.Trim();
            var telegram = client.ClientTg?.Trim();
            var whatsapp = client.ClientWa?.Trim();

            // Email
            if (!string.IsNullOrWhiteSpace(email))
            {
                notifications.Add(CreateQueuedNotification(client.Id, "email", email, subject, message));
            }

            // Telegram: отдельное поле клиента
            if (!string.IsNullOrWhiteSpace(telegram))
            {
                notifications.Add(CreateQueuedNotification(client.Id, "telegram", telegram, subject, message));
            }

            // WhatsApp: отдельное поле клиента
            if (!string.IsNullOrWhiteSpace(whatsapp))
            {
                notifications.Add(CreateQueuedNotification(client.Id, "whatsapp", whatsapp, subject, message));
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
            var notification = CreateQueuedNotification(
                clientId,
                channel,
                contactInfo,
                subject,
                message,
                metadata);

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Создано уведомление {NotificationId} для канала {Channel}",
                notification.Id,
                channel);

            return notification;
        }

        private static Notification CreateQueuedNotification(
            string? clientId,
            string channel,
            string contactInfo,
            string? subject,
            string message,
            string? metadata = null,
            DateTime? createdAt = null)
        {
            return new Notification
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = clientId,
                Channel = channel.ToLowerInvariant(),
                ContactInfo = contactInfo.Trim(),
                Subject = subject,
                Message = message,
                Status = "new",
                CreatedAt = createdAt ?? ParsersHelper.NowForTimestamp(),
                RetryCount = 0,
                Metadata = metadata
            };
        }

        private static string? ResolveContactInfo(OrganizationClient client, string channel)
        {
            return channel switch
            {
                "email" => client.ClientEmail?.Trim(),
                "telegram" => client.ClientTg?.Trim(),
                "whatsapp" => client.ClientWa?.Trim(),
                _ => null
            };
        }

        private static string BuildManualNotificationMetadata(string? createdBy, string channel, DateTime createdAt)
        {
            return JsonSerializer.Serialize(new
            {
                source = "manual",
                createdBy,
                channel,
                createdAt
            });
        }

        private async Task RemovePendingAppointmentReminderNotificationsAsync(
            string appointmentId,
            CancellationToken cancellationToken)
        {
            var existingNotifications = await _db.Notifications
                .Where(x =>
                    x.Status == "new" &&
                    x.Metadata != null &&
                    EF.Functions.Like(x.Metadata, "%\"source\":\"appointment_reminder\"%") &&
                    EF.Functions.Like(x.Metadata, $"%\"appointmentId\":\"{appointmentId}\"%"))
                .ToListAsync(cancellationToken);

            if (existingNotifications.Count == 0)
            {
                return;
            }

            _db.Notifications.RemoveRange(existingNotifications);
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static string BuildAppointmentReminderMetadata(
            Appointment appointment,
            OrganizationClient client,
            User? doctor,
            DateTime sendNotBefore)
        {
            return JsonSerializer.Serialize(new
            {
                source = "appointment_reminder",
                appointmentId = appointment.Id,
                organizationId = appointment.OrganizationId,
                patientId = client.Id,
                patientName = client.ClientName,
                doctorId = doctor?.Id,
                doctorName = doctor?.Name,
                startsAt = appointment.StartsAt,
                sendNotBefore
            });
        }
    }
}
