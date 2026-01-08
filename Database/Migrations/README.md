# Миграции базы данных

## Создание таблицы notifications

Для создания таблицы уведомлений выполните SQL скрипт:

```bash
psql -h 77.73.69.143 -U secore -d secore -f Database/Migrations/CreateNotificationsTable.sql
```

Или выполните SQL вручную:

```sql
-- Создание таблицы notifications для системы уведомлений
CREATE TABLE IF NOT EXISTS notifications (
    id VARCHAR PRIMARY KEY,
    client_id VARCHAR,
    channel VARCHAR NOT NULL,
    contact_info VARCHAR NOT NULL,
    subject VARCHAR,
    message TEXT,
    status VARCHAR DEFAULT 'new',
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    processed_at TIMESTAMP WITHOUT TIME ZONE,
    sent_at TIMESTAMP WITHOUT TIME ZONE,
    retry_count INTEGER DEFAULT 0,
    error_message TEXT,
    metadata TEXT,
    CONSTRAINT notifications_fk FOREIGN KEY (client_id) REFERENCES organization_clients(id)
);

-- Комментарии к полям
COMMENT ON TABLE notifications IS 'Таблица уведомлений для отправки через различные каналы';
COMMENT ON COLUMN notifications.channel IS 'Канал отправки: email, telegram, whatsapp';
COMMENT ON COLUMN notifications.contact_info IS 'Контактная информация для отправки (email, телефон, telegram chat_id)';
COMMENT ON COLUMN notifications.status IS 'new - новое, ожидает обработки; processing - в процессе обработки; sent - отправлено; failed - ошибка отправки';
COMMENT ON COLUMN notifications.metadata IS 'Дополнительные данные в формате JSON';

-- Индексы для оптимизации запросов
CREATE INDEX IF NOT EXISTS idx_notifications_status ON notifications(status);
CREATE INDEX IF NOT EXISTS idx_notifications_client_id ON notifications(client_id);
CREATE INDEX IF NOT EXISTS idx_notifications_created_at ON notifications(created_at);
CREATE INDEX IF NOT EXISTS idx_notifications_channel ON notifications(channel);
```

## Проверка

После выполнения миграции проверьте создание таблицы:

```sql
SELECT * FROM notifications LIMIT 10;
```

