# SECORE — единая схема файловых логов (вне контейнера)

Логи **трёх сервисов** хранятся на хосте (не внутри Docker-слоя), чтобы переживали redeploy:

```text
/secore/
  mainSystem/     ← WebApplication1 (этот репозиторий)
  notificator/    ← сервис уведомлений
  sheduler/       ← PaymentSheduler / планировщик
```

Файлы **по дням**, с датой/временем в каждой строке:

```text
/secore/mainSystem/mainSystem-20260722.log
/secore/notificator/notificator-20260722.log
/secore/sheduler/sheduler-20260722.log
```

Формат строки:

```text
2026-07-22 15:40:01.123 [INF] [SourceContext] Message...
```

## MainSystem (уже настроено здесь)

- Serilog → Console + File (`RollingInterval.Day`)
- Путь: `SecoreLogging:RootPath` (по умолчанию `/secore/mainSystem`)
- Docker volume: `-v /secore/mainSystem:/secore/mainSystem`
- Middleware логирует HTTP (особенно POST/API/ошибки)
- Бизнес-события: login/logout, create/update client, create invoices, connector pay

Локально (Development) по умолчанию: `./logs/mainSystem/`

---

## Промпт для Notificator и Sheduler

Скопируй блок ниже в чат агента в репозитории **Notificator** и отдельно в репозитории **PaymentSheduler / Sheduler**.

````markdown
# Задача: файловые логи SECORE вне контейнера

Нужно переделать логирование так же, как в mainSystem (WebApplication1 / SECORE billing).

## Цель
Логи НЕ должны жить только внутри Docker-контейнера. При обновлении/пересоздании контейнера логи должны сохраняться на хосте.

## Структура на сервере (Linux VPS)
```text
/secore/notificator/   — если это Notificator
/secore/sheduler/      — если это PaymentSheduler / Sheduler
```

Имена сервисов строго:
- Notificator → папка `notificator`, префикс файла `notificator`
- Scheduler → папка `sheduler` (орфография как у нас в инфраструктуре), префикс файла `sheduler`

Файлы по дням:
```text
/secore/notificator/notificator-YYYYMMDD.log
/secore/sheduler/sheduler-YYYYMMDD.log
```
(Serilog File sink с `rollingInterval: Day` и путём `.../notificator-.log` даёт такие имена.)

## Требования к содержимому логов
1. Каждая запись строго с датой и временем: `yyyy-MM-dd HH:mm:ss.fff`
2. Уровень: INF / WRN / ERR
3. SourceContext (класс/сервис)
4. Логировать все основные моменты:
   - старт/стоп приложения
   - успешные и неуспешные операции отправки уведомлений / обработки платежей
   - ошибки с exception stack
   - важные входные идентификаторы (orgId, clientId, invoiceId, txnId и т.д.) — без паролей и секретов
5. Не засорять лог: Microsoft.*/EF Core → Warning и выше

## Техника (если .NET)
- Использовать Serilog.AspNetCore (или уже есть — настроить)
- Конфиг секция:
```json
"SecoreLogging": {
  "RootPath": "/secore/notificator",
  "FilePrefix": "notificator",
  "RetainedFileCountLimit": 120,
  "MinimumLevel": "Information"
}
```
Для scheduler соответственно `RootPath=/secore/sheduler`, `FilePrefix=sheduler`.
- В Development можно писать в `./logs/<serviceName>/`
- На старте: `Directory.CreateDirectory(rootPath)`
- Output template:
```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}
```
- `Log.CloseAndFlush()` при остановке

## Docker / deploy
1. На хосте перед запуском:
```bash
sudo mkdir -p /secore/notificator /secore/sheduler /secore/mainSystem
sudo chmod -R 755 /secore
```
2. В `docker run` (или compose) смонтировать volume:
```bash
# Notificator
-v /secore/notificator:/secore/notificator \
-e SecoreLogging__RootPath=/secore/notificator \
-e SecoreLogging__FilePrefix=notificator

# Sheduler
-v /secore/sheduler:/secore/sheduler \
-e SecoreLogging__RootPath=/secore/sheduler \
-e SecoreLogging__FilePrefix=sheduler
```
3. Обновить GitHub Actions / deploy script аналогично.

## Definition of done
- [ ] После redeploy контейнера старые лог-файлы на диске остаются
- [ ] За текущий день появляется файл `*-YYYYMMDD.log`
- [ ] В файле есть строки со стартом сервиса и ключевыми бизнес-событиями с датой/временем
- [ ] Секреты (пароли, connection string, токены) в лог не пишутся
````
