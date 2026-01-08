namespace WebApplication1.Services
{
    /// <summary>
    /// Интерфейс для каналов уведомлений
    /// </summary>
    public interface INotificationChannel
    {
        /// <summary>
        /// Проверяет доступность канала для клиента
        /// </summary>
        bool IsAvailable(string? contactInfo);

        /// <summary>
        /// Отправляет уведомление
        /// </summary>
        Task<bool> SendAsync(string? contactInfo, string subject, string message);
    }
}

