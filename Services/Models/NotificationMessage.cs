namespace WebApplication1.Services.Models
{
    /// <summary>
    /// Стандартный формат сообщения для RabbitMQ
    /// </summary>
    public class NotificationMessage
    {
        /// <summary>
        /// ID уведомления в БД
        /// </summary>
        public string NotificationId { get; set; } = null!;

        /// <summary>
        /// ID клиента
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Канал отправки: email, telegram, whatsapp
        /// </summary>
        public string Channel { get; set; } = null!;

        /// <summary>
        /// Контактная информация (email, телефон, chat_id)
        /// </summary>
        public string ContactInfo { get; set; } = null!;

        /// <summary>
        /// Тема уведомления
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        /// Тело сообщения
        /// </summary>
        public string Message { get; set; } = null!;

        /// <summary>
        /// Дополнительные данные в формате JSON
        /// </summary>
        public string? Metadata { get; set; }
    }
}

