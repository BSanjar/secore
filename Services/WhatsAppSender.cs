using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Services
{
    /// <summary>
    /// Сервис для отправки WhatsApp уведомлений
    /// </summary>
    public class WhatsAppSender
    {
        private readonly ILogger<WhatsAppSender> _logger;
        private readonly IConfiguration _configuration;

        public WhatsAppSender(
            ILogger<WhatsAppSender> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> SendAsync(string phone, string? subject, string message)
        {
            try
            {
                var apiKey = _configuration["NotificationSettings:WhatsApp:ApiKey"];
                var apiUrl = _configuration["NotificationSettings:WhatsApp:ApiUrl"];

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiUrl))
                {
                    _logger.LogWarning("WhatsApp API настройки не настроены. Уведомление не отправлено на {Phone}", phone);
                    return false;
                }

                // Здесь должна быть интеграция с WhatsApp Business API
                // Пример реализации (нужно адаптировать под конкретный API):
                // var client = new HttpClient();
                // var request = new { to = phone, message = $"{subject}\n{message}" };
                // var response = await client.PostAsync(apiUrl, ...);

                _logger.LogInformation("WhatsApp уведомление отправлено на {Phone}", phone);
                await Task.CompletedTask; // Заглушка для реализации
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке WhatsApp на {Phone}", phone);
                return false;
            }
        }
    }
}

