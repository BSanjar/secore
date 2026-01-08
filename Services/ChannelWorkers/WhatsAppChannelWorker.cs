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
    /// Воркер для обработки WhatsApp уведомлений из RabbitMQ
    /// </summary>
    public class WhatsAppChannelWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WhatsAppChannelWorker> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;

        public WhatsAppChannelWorker(
            IServiceProvider serviceProvider,
            ILogger<WhatsAppChannelWorker> logger,
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
            _logger.LogInformation("WhatsAppChannelWorker запущен");

            _rabbitMQService.Subscribe("whatsapp", async (message) =>
            {
                return await ProcessWhatsAppNotificationAsync(message);
            });

            return Task.CompletedTask;
        }

        private async Task<bool> ProcessWhatsAppNotificationAsync(NotificationMessage message)
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
                var apiKey = _configuration["NotificationSettings:WhatsApp:ApiKey"];
                var apiUrl = _configuration["NotificationSettings:WhatsApp:ApiUrl"];

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiUrl))
                {
                    _logger.LogWarning("WhatsApp API настройки не настроены. Уведомление {NotificationId} не отправлено.", message.NotificationId);
                    notification.Status = "failed";
                    notification.ErrorMessage = "WhatsApp API настройки не настроены";
                    await db.SaveChangesAsync();
                    return false;
                }

                // Здесь должна быть интеграция с WhatsApp Business API
                // Пример реализации (нужно адаптировать под конкретный API):
                // var client = new HttpClient();
                // var request = new { to = message.ContactInfo, message = message.Message };
                // var response = await client.PostAsync(apiUrl, ...);

                // Для примера - заглушка
                _logger.LogInformation("WhatsApp уведомление {NotificationId} отправлено на {Phone}", 
                    message.NotificationId, message.ContactInfo);

                // Обновляем статус уведомления
                notification.Status = "sent";
                notification.SentAt = DateHelper.NowForTimestamp();
                notification.ErrorMessage = null;
                await db.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке WhatsApp уведомления {NotificationId}", message.NotificationId);

                notification.Status = "failed";
                notification.ErrorMessage = ex.Message;
                notification.RetryCount = (notification.RetryCount ?? 0) + 1;
                await db.SaveChangesAsync();

                return false;
            }
        }
    }
}

