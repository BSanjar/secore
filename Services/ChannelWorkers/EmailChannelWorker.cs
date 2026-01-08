using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Models;
using WebApplication1.Helpers;

namespace WebApplication1.Services.ChannelWorkers
{
    /// <summary>
    /// Воркер для обработки Email уведомлений из RabbitMQ
    /// </summary>
    public class EmailChannelWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailChannelWorker> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;

        public EmailChannelWorker(
            IServiceProvider serviceProvider,
            ILogger<EmailChannelWorker> logger,
            IRabbitMQService rabbitMQService,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _rabbitMQService = rabbitMQService;
            _configuration = configuration;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailChannelWorker запущен");

            _rabbitMQService.Subscribe("email", async (message) =>
            {
                return await ProcessEmailNotificationAsync(message);
            });

            return Task.CompletedTask;
        }

        private async Task<bool> ProcessEmailNotificationAsync(NotificationMessage message)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.Id == message.NotificationId);

            if (notification == null)
            {
                _logger.LogWarning("Уведомление {NotificationId} не найдено в БД", message.NotificationId);
                return false;
            }

            try
            {
                var smtpHost = _configuration["NotificationSettings:Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["NotificationSettings:Email:SmtpPort"] ?? "587");
                var smtpUser = _configuration["NotificationSettings:Email:SmtpUser"];
                var smtpPassword = _configuration["NotificationSettings:Email:SmtpPassword"];
                var fromEmail = _configuration["NotificationSettings:Email:FromEmail"] ?? smtpUser;

                if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPassword))
                {
                    _logger.LogWarning("SMTP настройки не настроены. Уведомление {NotificationId} не отправлено.", message.NotificationId);
                    notification.Status = "failed";
                    notification.ErrorMessage = "SMTP настройки не настроены";
                    await db.SaveChangesAsync();
                    return false;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPassword)
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail ?? "noreply@secore.kg"),
                    Subject = message.Subject ?? "Уведомление",
                    Body = message.Message,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(message.ContactInfo);

                await client.SendMailAsync(mailMessage);

                // Обновляем статус уведомления
                notification.Status = "sent";
                notification.SentAt = DateHelper.NowForTimestamp();
                notification.ErrorMessage = null;
                await db.SaveChangesAsync();

                _logger.LogInformation("Email уведомление {NotificationId} отправлено на {Email}", 
                    message.NotificationId, message.ContactInfo);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке email уведомления {NotificationId}", message.NotificationId);

                notification.Status = "failed";
                notification.ErrorMessage = ex.Message;
                notification.RetryCount = (notification.RetryCount ?? 0) + 1;
                await db.SaveChangesAsync();

                return false;
            }
        }
    }
}

