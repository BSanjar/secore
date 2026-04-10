# План миграции: от `Areas/*` к **Core + Modules + Theme (Front)**

Цель: сохранить **одно ядро** (общие сущности/БД/сервисы/маршруты) и дать возможность иметь:
- разные UI (layout/views/CSS/JS) для разных типов организаций,
- небольшие отклонения бизнес-логики в отдельных местах,
при этом **в перспективе полностью убрать ASP.NET MVC Areas** из URL и структуры контроллеров.

Ниже план, привязанный к текущей реализации проекта.

---

## Текущее состояние (что уже важно учитывать)

### 1) Маршрутизация
- В `Program.cs` используется маршрут Areas:
  - `"{area:exists}/{controller=Cabinet}/{action=Index}/{id?}"`
- Редиректы после логина/с главной завязаны на `GetAreaByOrganizationType()` (дублируется в:
  - `Controllers/AccountController.cs`
  - `Controllers/HomeController.cs`)

### 2) Layout для страниц вне Area
- `Filters/SetCabinetLayoutFilter.cs` кладёт в сессию `CabinetLayout = area` (только для ограниченного списка Area).
- `Views/_ViewStart.cshtml` выбирает layout по `Session["CabinetLayout"]` через `if/else` switch.

### 3) Тип организации
- В сессии уже хранится `OrganizationType` (ставится в `AccountController.Login`).
- Права (`PermissionsController`) фильтруются по `Permission.Area == null || Permission.Area == organizationType`.

---

## Целевая архитектура (что хотим получить)

### 1) Variant (вариант продукта) вместо Area
**Variant** = нормализованный `OrganizationType` (например `detsad`, `simple`, `standart`, `medclinic`).

- Variant используется:
  - для выбора реализаций модульной логики (Modules),
  - для выбора темы UI (Theme),
  - для фильтрации permissions (можно оставить как есть, но мыслить как Variant).

### 2) Theme (Front)
UI выбирается по Variant:
- layout: `Views/Themes/{variant}/_LayoutCabinet.cshtml`
- view overrides (опционально): `Views/Themes/{variant}/{Controller}/{View}.cshtml`
- если override не найден — используется дефолтный view из `Views/{Controller}/{View}.cshtml`

### 3) Modules (отклонения в логике бэка)
Отклонения не живут в контроллерах через `if (orgType == ...)`.
Они живут в реализациях интерфейсов, которые выбираются по Variant.

Примеры интерфейсов-политик (в Core/Contracts):
- `IPayCodePolicy` — правила выбора/генерации лицевого счёта при создании invoice
- `IClientDisplayPolicy` — терминология/поля (например «ребёнок» vs «клиент»)
- `IInvoiceDefaultsPolicy` — автопролонгация/периодичность по умолчанию
- `IStatementTemplateProvider` — шаблоны PDF/квитанций

---

## Этапы миграции (итеративно, без “переписать всё”)

### Этап 0. Подготовка (минимальные изменения, без ломки)
**Цель:** перестать размножать маппинг `OrganizationType → Area`, начать мыслить “Variant”.

1) Ввести единый резолвер Variant
- Добавить сервис `ICurrentVariantAccessor` (или `CurrentVariant`) который читает Variant из:
  - `HttpContext.Session["OrganizationType"]` (пока так),
  - при желании — из claims (в будущем).
- Добавить `Variant` как синоним `OrganizationType` (на первом шаге это одно и то же).

2) Убрать дублирование `GetAreaByOrganizationType()`
- Вынести маппинг в одно место (например `CabinetRouteHelper` или `VariantRouting`).
- `AccountController` и `HomeController` должны вызывать один общий метод.

3) Подготовить структуру тем
- Создать `Views/Themes/detsad`, `Views/Themes/simple`, `Views/Themes/standart`, `Views/Themes/medclinic`.
- Для начала: только layout-файлы в темах (без переопределения страниц).

**Критерий готовности:** проект работает как раньше, но маппинг и “variant” централизованы.

---

### Этап 1. Перевести выбор layout с `CabinetLayout=Area` на `Theme=Variant`
**Цель:** UI определяется Variant, а не “какой area в маршруте”.

1) Перестать использовать `SetCabinetLayoutFilter` как источник истины
- Ввести `CabinetTheme` в сессию (или вычислять на лету из Variant).
- Логика: `CabinetTheme = Variant` (или отдельная таблица соответствий).

2) Упростить `Views/_ViewStart.cshtml`
- Вместо `if (cabinetLayout == "Detsad") ...`:
  - читать `CabinetTheme` (или Variant),
  - ставить layout по соглашению: `~/Views/Themes/{theme}/_LayoutCabinet.cshtml`,
  - fallback: `_Layout`.

3) Подключение статики (CSS/JS) перенести в layout темы
- detsad layout подключает detsad css/js
- simple layout подключает simple css/js
- общие файлы остаются общими.

**Критерий готовности:** даже если URL/маршрут прежний, layout выбирается по Variant, а не по Area.

---

### Этап 2. Theme View Overrides (перевести “разный фронт” на темы)
**Цель:** разные страницы/partials можно переопределять темами без Areas.

1) Добавить `IViewLocationExpander` (ThemeViewLocationExpander)
- Он добавляет пути поиска:
  - `Views/Themes/{theme}/{Controller}/{View}.cshtml`
  - `Views/Themes/{theme}/Shared/{View}.cshtml`
  - затем стандартные пути.

2) Начать перенос UI-отличий из `Areas/*/Views` в `Views/Themes/*`
- Выбирать “пилотный” экран (например Detsad Cabinet Children/Invoices) и перенести его view в тему.
- Общие экраны оставить в обычных `Views/*`.

**Критерий готовности:** при одном и том же контроллере/экшене разные варианты получают разные view.

---

### Этап 3. Modules: вынести различия бэка в политики (без Areas)
**Цель:** контроллеры и core-сервисы не содержат `if (variant == ...)`.

1) Ввести “политики” для наиболее конфликтных мест
Рекомендуемый порядок:
- `IPayCodePolicy` (у вас уже есть режимы new/duplicate/both)
- `IInvoiceDefaultsPolicy` (autoProlongation + periodicity defaults)
- `IClientDisplayPolicy` (“ребёнок” vs “клиент”, label’ы, тексты, набор полей)

2) Реализации политик по Variant
- `Modules/Detsad/*Policy.cs`
- `Modules/Simple/*Policy.cs`
- `Modules/Standart/*Policy.cs` (дефолт)

3) Резолвер политик по Variant
- Один `VariantPolicyResolver`:
  - на вход: Variant + тип политики,
  - на выход: реализация из DI,
  - кэш на время запроса через `HttpContext.Items`.

4) Подключение в сервисы
- Например `OperationsByInvoices` и `ClientService` начинают принимать политики (через DI),
  чтобы поведение было вариативным без ветвлений в контроллерах.

**Критерий готовности:** отличия логики достигаются заменой политики, а не дублированием контроллеров в Area.

---

### Этап 4. “De-Area” — убрать Areas из контроллеров и URL
**Цель:** убрать `Areas/*/Controllers`, `MapControllerRoute("{area:exists}/...")`, и перейти на единые маршруты.

1) Создать новые “единые” контроллеры в `Controllers/` (Core UI)
- Например:
  - `CabinetController` (общий)
  - `InvoicesController` (общий)
  - `PaymentsController` (общий)
- Они используют:
  - core-сервисы,
  - политики (modules),
  - theme views (front).

2) Сохранить обратную совместимость URL
На переходный период:
- оставить area-route, но внутри редиректить/проксировать на новый маршрут, или
- добавить новые маршруты вида `/Cabinet/...` параллельно со старыми.

3) Перенести действия из `Areas/Detsad/...` и `Areas/Simple/...` в общие контроллеры
- Переносить по одному экрану/контроллеру.
- После переноса:
  - старый area-контроллер может временно делать `RedirectToAction` на новый.

4) Удалить area-route и `Areas/*/Controllers` после миграции всех экранов
- Удалить в `Program.cs`:
  - `app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Cabinet}/{action=Index}/{id?}");`
- Очистить `SetCabinetLayoutFilter` (скорее всего станет не нужен).

**Критерий готовности:** приложение работает без Areas в URL и без area-контроллеров.

---

## Рекомендованный “минимальный пилот” (чтобы быстро увидеть эффект)

1) Этап 1: Theme layout вместо `CabinetLayout`
- Быстро и сразу убирает завязку UI на Area.

2) Этап 2: Theme overrides для 1-2 страниц Detsad
- Например `Cabinet/Children` и `Invoices/Index`.

3) Этап 3: `IPayCodePolicy` и `IInvoiceDefaultsPolicy`
- Это прямо соответствует вашему текущему функционалу (payCodeMode, autoProlongation defaults).

После этого добавление нового “фронта” для нового типа организации становится:
- добавить theme layout + нужные view overrides,
- добавить (при необходимости) реализации политик в modules,
без копирования контроллеров/вьюх в новую Area.

---

## Риски и как их контролировать

- **Смешивание Variant и Area в переходный период**
  - Решение: Variant всегда источник истины, Area — временный маршрут.

- **Permissions.Area сейчас хранит `organizationType`**
  - Это нормально, но важно не путать с MVC Area.
  - В документации/коде использовать термин “Variant”.

- **Много UI отличий**
  - Не переносить всё сразу: Theme overrides точечно, остальное fallback на дефолт.

---

## Definition of Done (конечная цель)

- Нет маршрута `{area:exists}` в `Program.cs`.
- Нет `Areas/*/Controllers` (или они только для совместимости и потом удалены).
- UI определяется `Theme` (по Variant), а не по Area.
- Отклонения логики реализованы через политики (Modules), а не через ветвления в контроллерах/вьюхах.

