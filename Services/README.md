# Сервисы уведомлений и автоматических платежей

## Архитектура системы уведомлений

Система уведомлений построена на основе очередей RabbitMQ и работает следующим образом:

1. **Создание уведомлений** - различные части системы (монолит, job, внешние API) создают записи в таблице `notifications` со статусом `new`
2. **NotificationWorker** - фоновый воркер периодически выбирает записи со статусом `new`, формирует сообщения и публикует их в RabbitMQ
3. **RabbitMQ** - принимает сообщения и доставляет их в очереди соответствующих каналов
4. **Канальные воркеры** - отдельные воркеры для каждого канала (Email, Telegram, WhatsApp) слушают свои очереди, отправляют уведомления и обновляют статус в БД

## Компоненты

### 1. NotificationService

Сервис для создания уведомлений в БД. Используется различными частями системы для создания уведомлений.

**Методы:**
- `CreateOverduePaymentNotificationAsync` - создаёт уведомление о просроченном платеже
- `CreateInsufficientBalanceNotificationAsync` - создаёт уведомление о недостаточном балансе
- `CreateAutoPaymentSuccessNotificationAsync` - создаёт уведомление об успешном автоплатеже
- `CreateNotificationAsync` - создаёт уведомление напрямую (для внешних API)

### 2. NotificationWorker

Фоновый воркер (BackgroundService), который:
- Проверяет таблицу `notifications` каждые 10 секунд
- Выбирает пачку записей (до 50) со статусом `new`
- Обновляет статус на `processing`
- Формирует сообщения стандартного формата
- Публикует их в RabbitMQ через соответствующие очереди

### 3. RabbitMQService

Сервис для работы с RabbitMQ:
- Создаёт exchange `notifications` типа Direct
- Создаёт очереди для каждого канала: `notifications_email`, `notifications_telegram`, `notifications_whatsapp`
- Публикует сообщения в соответствующие очереди
- Подписывается на очереди для обработки сообщений

### 4. Канальные воркеры

Отдельные фоновые воркеры для каждого канала:

#### EmailChannelWorker
- Слушает очередь `notifications_email`
- Отправляет уведомления через SMTP
- Обновляет статус уведомления в БД на `sent` или `failed`

#### TelegramChannelWorker
- Слушает очередь `notifications_telegram`
- Отправляет уведомления через Telegram Bot API
- Обновляет статус уведомления в БД

#### WhatsAppChannelWorker
- Слушает очередь `notifications_whatsapp`
- Отправляет уведомления через WhatsApp Business API
- Обновляет статус уведомления в БД

## Модель данных

### Таблица notifications

```sql
CREATE TABLE notifications (
    id VARCHAR PRIMARY KEY,
    client_id VARCHAR,
    channel VARCHAR NOT NULL,           -- email, telegram, whatsapp
    contact_info VARCHAR NOT NULL,      -- email, телефон, chat_id
    subject VARCHAR,
    message TEXT,
    status VARCHAR DEFAULT 'new',       -- new, processing, sent, failed
    created_at TIMESTAMP,
    processed_at TIMESTAMP,
    sent_at TIMESTAMP,
    retry_count INTEGER DEFAULT 0,
    error_message TEXT,
    metadata TEXT                        -- JSON для дополнительных данных
);
```

### Статусы уведомлений

- `new` - новое уведомление, ожидает обработки
- `processing` - в процессе обработки (опубликовано в RabbitMQ)
- `sent` - успешно отправлено
- `failed` - ошибка отправки (после 5 попыток)

## Формат сообщения RabbitMQ

```json
{
  "NotificationId": "guid",
  "ClientId": "client-id",
  "Channel": "email|telegram|whatsapp",
  "ContactInfo": "email@example.com|+996...|@username",
  "Subject": "Тема уведомления",
  "Message": "Тело сообщения (HTML или текст)",
  "Metadata": "{\"key\":\"value\"}"
}
```

## Настройка

### appsettings.json

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": "5672",
    "UserName": "guest",
    "Password": "guest"
  },
  "NotificationSettings": {
    "Email": {
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": "587",
      "SmtpUser": "your-email@gmail.com",
      "SmtpPassword": "your-app-password",
      "FromEmail": "noreply@yourdomain.com"
    },
    "Telegram": {
      "BotToken": "your-bot-token"
    },
    "WhatsApp": {
      "ApiKey": "your-api-key",
      "ApiUrl": "https://api.whatsapp.com/..."
    }
  }
}
```

## Использование

### Создание уведомления из контроллера

```csharp
public class MyController : Controller
{
    private readonly NotificationService _notificationService;
    
    public MyController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }
    
    public async Task<IActionResult> SendNotification()
    {
        var notification = await _notificationService.CreateNotificationAsync(
            clientId: "client-id",
            channel: "email",
            contactInfo: "user@example.com",
            subject: "Важное уведомление",
            message: "<h1>Текст уведомления</h1>"
        );
        
        return Ok(notification);
    }
}
```

### Создание уведомления из внешнего API

```csharp
// POST /api/notifications
{
  "clientId": "client-id",
  "channel": "email",
  "contactInfo": "user@example.com",
  "subject": "Уведомление",
  "message": "Текст",
  "metadata": "{\"source\":\"external-api\"}"
}
```

## Логирование

Все компоненты логируют свои операции:
- Создание уведомлений
- Публикация в RabbitMQ
- Обработка сообщений из очередей
- Успешная отправка или ошибки

## Обработка ошибок

- При ошибке публикации в RabbitMQ статус возвращается на `new` для повторной попытки
- При ошибке отправки через канал статус обновляется на `failed` после 5 попыток
- Информация об ошибках сохраняется в поле `error_message`

## InvoicePaymentService

Сервис для обработки автоматических платежей использует `NotificationService` для создания уведомлений:
- При недостаточном балансе - создаёт уведомления о необходимости пополнения
- При просрочке - создаёт уведомления о просроченном платеже
- После успешного автоплатежа - создаёт уведомления об успешной оплате

Все уведомления обрабатываются асинхронно через систему очередей.
