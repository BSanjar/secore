using System.Net;
using System.Net.Mail;

namespace WebApplication1.Services
{
    /// <summary>
    /// Канал уведомлений через Email
    /// </summary>
    public class EmailNotificationChannel : INotificationChannel
    {
        private readonly ILogger<EmailNotificationChannel> _logger;
        private readonly IConfiguration _configuration;

        public EmailNotificationChannel(ILogger<EmailNotificationChannel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public bool IsAvailable(string? contactInfo)
        {
            return !string.IsNullOrWhiteSpace(contactInfo) && 
                   contactInfo.Contains("@") && 
                   IsValidEmail(contactInfo);
        }

        public async Task<bool> SendAsync(string? contactInfo, string subject, string message)
        {
            if (!IsAvailable(contactInfo))
            {
                _logger.LogWarning("Email не доступен для отправки: {Email}", contactInfo);
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
                    _logger.LogWarning("SMTP настройки не настроены. Уведомление не отправлено.");
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
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(contactInfo!);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email уведомление отправлено на {Email}", contactInfo);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке email на {Email}", contactInfo);
                return false;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}

