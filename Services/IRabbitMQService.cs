using WebApplication1.Services.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Интерфейс для работы с RabbitMQ
    /// </summary>
    public interface IRabbitMQService
    {
        /// <summary>
        /// Публикует сообщение в очередь указанного канала
        /// </summary>
        Task PublishAsync(NotificationMessage message);

        /// <summary>
        /// Подписывается на очередь канала и обрабатывает сообщения
        /// </summary>
        void Subscribe(string channel, Func<NotificationMessage, Task<bool>> handler);
    }
}

