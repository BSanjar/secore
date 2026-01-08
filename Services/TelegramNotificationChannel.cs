using System.Text;
using System.Text.Json;

namespace WebApplication1.Services
{
    /// <summary>
    /// Канал уведомлений через Telegram
    /// </summary>
    public class TelegramNotificationChannel : INotificationChannel
    {
        private readonly ILogger<TelegramNotificationChannel> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TelegramNotificationChannel(
            ILogger<TelegramNotificationChannel> logger, 
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public bool IsAvailable(string? contactInfo)
        {
            // Предполагаем, что contactInfo содержит Telegram chat_id или username
            // В реальной системе это должно храниться в отдельном поле или таблице
            return !string.IsNullOrWhiteSpace(contactInfo) && 
                   (contactInfo.StartsWith("@") || long.TryParse(contactInfo, out _));
        }

        public async Task<bool> SendAsync(string? contactInfo, string subject, string message)
        {
            if (!IsAvailable(contactInfo))
            {
                _logger.LogWarning("Telegram не доступен для отправки: {Contact}", contactInfo);
                return false;
            }

            try
            {
                var botToken = _configuration["NotificationSettings:Telegram:BotToken"];
                var chatId = contactInfo;

                if (string.IsNullOrWhiteSpace(botToken))
                {
                    _logger.LogWarning("Telegram Bot Token не настроен. Уведомление не отправлено.");
                    return false;
                }

                var fullMessage = $"*{subject}*\n\n{message}";
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
                    _logger.LogInformation("Telegram уведомление отправлено на {ChatId}", chatId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Ошибка отправки Telegram: {Response}", responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке Telegram на {Contact}", contactInfo);
                return false;
            }
        }
    }
}

