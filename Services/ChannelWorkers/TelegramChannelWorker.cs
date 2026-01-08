using System.Net.Http;
using System.Text;
using System.Text.Json;
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
    /// Воркер для обработки Telegram уведомлений из RabbitMQ
    /// </summary>
    public class TelegramChannelWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelegramChannelWorker> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TelegramChannelWorker(
            IServiceProvider serviceProvider,
            ILogger<TelegramChannelWorker> logger,
            IRabbitMQService rabbitMQService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _rabbitMQService = rabbitMQService;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TelegramChannelWorker запущен");

            _rabbitMQService.Subscribe("telegram", async (message) =>
            {
                return await ProcessTelegramNotificationAsync(message);
            });

            return Task.CompletedTask;
        }

        private async Task<bool> ProcessTelegramNotificationAsync(NotificationMessage message)
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
                var botToken = _configuration["NotificationSettings:Telegram:BotToken"];

                if (string.IsNullOrWhiteSpace(botToken))
                {
                    _logger.LogWarning("Telegram Bot Token не настроен. Уведомление {NotificationId} не отправлено.", message.NotificationId);
                    notification.Status = "failed";
                    notification.ErrorMessage = "Telegram Bot Token не настроен";
                    await db.SaveChangesAsync();
                    return false;
                }

                var chatId = message.ContactInfo;
                var fullMessage = $"*{message.Subject ?? "Уведомление"}*\n\n{message.Message}";
                var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

                var payload = new
                {
                    chat_id = chatId,
                    text = fullMessage,
                    parse_mode = "Markdown"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Обновляем статус уведомления
                    notification.Status = "sent";
                    notification.SentAt = DateHelper.NowForTimestamp();
                    notification.ErrorMessage = null;
                    await db.SaveChangesAsync();

                    _logger.LogInformation("Telegram уведомление {NotificationId} отправлено на {ChatId}", 
                        message.NotificationId, chatId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Ошибка отправки Telegram: {Response}", responseContent);
                    notification.Status = "failed";
                    notification.ErrorMessage = responseContent;
                    notification.RetryCount = (notification.RetryCount ?? 0) + 1;
                    await db.SaveChangesAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке Telegram уведомления {NotificationId}", message.NotificationId);

                notification.Status = "failed";
                notification.ErrorMessage = ex.Message;
                notification.RetryCount = (notification.RetryCount ?? 0) + 1;
                await db.SaveChangesAsync();

                return false;
            }
        }
    }
}

