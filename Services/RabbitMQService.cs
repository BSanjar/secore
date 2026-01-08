using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WebApplication1.Services.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Сервис для работы с RabbitMQ
    /// </summary>
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly string _exchangeName = "notifications";

        public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
        {
            _logger = logger;

            var hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
            var port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
            var userName = configuration["RabbitMQ:UserName"] ?? "guest";
            var password = configuration["RabbitMQ:Password"] ?? "guest";

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Создаём exchange для маршрутизации сообщений
            _channel.ExchangeDeclare(_exchangeName, ExchangeType.Direct, durable: true);

            // Создаём очереди для каждого канала
            var channels = new[] { "email", "telegram", "whatsapp" };
            foreach (var channel in channels)
            {
                var queueName = $"notifications_{channel}";
                _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(queueName, _exchangeName, channel);
            }

            _logger.LogInformation("RabbitMQ подключен: {HostName}:{Port}", hostName, port);
        }

        public Task PublishAsync(NotificationMessage message)
        {
            try
            {
                var routingKey = message.Channel.ToLower();
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true; // Сохраняем сообщения на диск

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation(
                    "Сообщение опубликовано в очередь {Queue}: NotificationId={NotificationId}",
                    routingKey,
                    message.NotificationId);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при публикации сообщения в RabbitMQ");
                throw;
            }
        }

        public void Subscribe(string channel, Func<NotificationMessage, Task<bool>> handler)
        {
            var queueName = $"notifications_{channel.ToLower()}";

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<NotificationMessage>(messageJson);

                if (message == null)
                {
                    _logger.LogWarning("Не удалось десериализовать сообщение из очереди {Queue}", queueName);
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                    return;
                }

                try
                {
                    _logger.LogInformation(
                        "Получено сообщение из очереди {Queue}: NotificationId={NotificationId}",
                        queueName,
                        message.NotificationId);

                    var success = await handler(message);

                    if (success)
                    {
                        _channel.BasicAck(ea.DeliveryTag, false);
                        _logger.LogInformation(
                            "Сообщение обработано успешно: NotificationId={NotificationId}",
                            message.NotificationId);
                    }
                    else
                    {
                        _channel.BasicNack(ea.DeliveryTag, false, true); // Повторная попытка
                        _logger.LogWarning(
                            "Ошибка обработки сообщения, будет повторная попытка: NotificationId={NotificationId}",
                            message.NotificationId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Исключение при обработке сообщения: NotificationId={NotificationId}",
                        message.NotificationId);
                    _channel.BasicNack(ea.DeliveryTag, false, true); // Повторная попытка
                }
            };

            _channel.BasicQos(0, 1, false); // Обрабатываем по одному сообщению за раз
            _channel.BasicConsume(queueName, autoAck: false, consumer);

            _logger.LogInformation("Подписка на очередь {Queue} установлена", queueName);
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}

