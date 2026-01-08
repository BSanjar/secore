using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.DBModels;
using WebApplication1.Services.Models;
using WebApplication1.Helpers;

namespace WebApplication1.Services
{
    /// <summary>
    /// Фоновый воркер, который обрабатывает таблицу notifications
    /// Выбирает записи со статусом "new" и публикует их в RabbitMQ
    /// </summary>
    public class NotificationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationWorker> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10); // Проверка каждые 10 секунд
        private readonly int _batchSize = 50; // Размер пачки для обработки

        public NotificationWorker(
            IServiceProvider serviceProvider,
            ILogger<NotificationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationWorker запущен. Проверка каждые {Interval} секунд", _checkInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNotificationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в NotificationWorker");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rabbitMQService = scope.ServiceProvider.GetRequiredService<IRabbitMQService>();

            // Выбираем пачку уведомлений со статусом "new"
            var notifications = await db.Notifications
                .Where(n => n.Status == "new")
                .OrderBy(n => n.CreatedAt)
                .Take(_batchSize)
                .ToListAsync(cancellationToken);

            if (!notifications.Any())
            {
                return; // Нет новых уведомлений
            }

            _logger.LogInformation("Найдено {Count} новых уведомлений для обработки", notifications.Count);

            foreach (var notification in notifications)
            {
                try
                {
                    // Обновляем статус на "processing"
                    notification.Status = "processing";
                    notification.ProcessedAt = DateHelper.NowForTimestamp();
                    await db.SaveChangesAsync(cancellationToken);

                    // Формируем сообщение стандартного формата
                    var message = new NotificationMessage
                    {
                        NotificationId = notification.Id,
                        ClientId = notification.ClientId,
                        Channel = notification.Channel ?? "email",
                        ContactInfo = notification.ContactInfo ?? "",
                        Subject = notification.Subject,
                        Message = notification.Message ?? "",
                        Metadata = notification.Metadata
                    };

                    // Публикуем в RabbitMQ
                    await rabbitMQService.PublishAsync(message);

                    _logger.LogInformation(
                        "Уведомление {NotificationId} опубликовано в RabbitMQ (канал: {Channel})",
                        notification.Id,
                        notification.Channel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Ошибка при обработке уведомления {NotificationId}",
                        notification.Id);

                    // Возвращаем статус обратно на "new" для повторной попытки
                    notification.Status = "new";
                    notification.ProcessedAt = null;
                    notification.RetryCount = (notification.RetryCount ?? 0) + 1;
                    notification.ErrorMessage = ex.Message;

                    if (notification.RetryCount > 5)
                    {
                        // После 5 попыток помечаем как failed
                        notification.Status = "failed";
                    }

                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationWorker останавливается");
            await base.StopAsync(cancellationToken);
        }
    }
}

