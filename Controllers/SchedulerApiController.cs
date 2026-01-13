using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    /// <summary>
    /// API контроллер для InvoiceSchedulerJob
    /// Предоставляет endpoints для обработки запланированных платежей
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SchedulerApiController : ControllerBase
    {
        private readonly InvoicePaymentService _paymentService;
        private readonly ILogger<SchedulerApiController> _logger;
        private readonly IConfiguration _configuration;

        public SchedulerApiController(
            InvoicePaymentService paymentService,
            ILogger<SchedulerApiController> logger,
            IConfiguration configuration)
        {
            _paymentService = paymentService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Проверка авторизации через Basic Authentication (логин/пароль)
        /// </summary>
        private bool ValidateCredentials()
        {
            var expectedUsername = _configuration["SchedulerApi:Username"];
            var expectedPassword = _configuration["SchedulerApi:Password"];

            if (string.IsNullOrWhiteSpace(expectedUsername) || string.IsNullOrWhiteSpace(expectedPassword))
            {
                _logger.LogWarning("SchedulerApi:Username или SchedulerApi:Password не настроены в appsettings.json");
                return false;
            }

            // Проверяем заголовок Authorization
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                _logger.LogWarning("Заголовок Authorization не предоставлен");
                return false;
            }

            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"].FirstOrDefault());
            if (authHeader?.Scheme != "Basic")
            {
                _logger.LogWarning("Неверная схема авторизации. Ожидается Basic");
                return false;
            }

            // Декодируем Base64
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter ?? "");
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            
            if (credentials.Length != 2)
            {
                _logger.LogWarning("Неверный формат credentials в Basic Auth");
                return false;
            }

            var providedUsername = credentials[0];
            var providedPassword = credentials[1];

            // Проверяем логин и пароль
            if (providedUsername != expectedUsername || providedPassword != expectedPassword)
            {
                _logger.LogWarning("Неверный логин или пароль для SchedulerApi");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Health check endpoint для проверки доступности API
        /// Health check не требует авторизации для упрощения проверки доступности
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Обработка запланированных платежей
        /// Вызывается InvoiceSchedulerJob для обработки просроченных и подошедших платежей
        /// </summary>
        [HttpPost("process-payments")]
        public async Task<IActionResult> ProcessPayments()
        {
            // Проверка авторизации (логин/пароль)
            if (!ValidateCredentials())
            {
                _logger.LogWarning("Неверные учетные данные при попытке обработки платежей");
                return Unauthorized(new { success = false, message = "Неверный логин или пароль" });
            }

            try
            {
                _logger.LogInformation("Получен запрос на обработку запланированных платежей от SchedulerJob");

                await _paymentService.ProcessScheduledPaymentsAsync();

                _logger.LogInformation("Обработка запланированных платежей завершена успешно");

                return Ok(new { success = true, message = "Платежи обработаны успешно", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запланированных платежей");

                return StatusCode(500, new { success = false, message = "Ошибка при обработке платежей", error = ex.Message });
            }
        }
    }
}
