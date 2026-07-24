-- =============================================================================
-- DEMO SEED DATA — Secore Billing
-- PostgreSQL. Все суммы в ТЫЙЫНАХ (1 сом = 100 тыйын).
-- Пароль всех пользователей: Demo123!
-- Хеш SHA-256 → Base64: WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=
--
-- Организации:
--   org-med-01     medclinic  — Медицинский центр «Здоровье Плюс»
--   01000  detsad     — Детский сад №15 «Балажан»
--   org-school-01  standart   — Школа №42 им. Абая (школьный биллинг)
--   org-simple-01  simple     — ИП «Касымов Сервис» (упрощённый кабинет)
--
-- Логины:
--   admin@zdorovie.kg / doctor@zdorovie.kg / registry@zdorovie.kg
--   admin@balazhan.kg / teacher@balazhan.kg
--   admin@school42.kg / accountant@school42.kg
--   admin@kasymov.kg
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Agent (платёжный коннектор, пароль API — plaintext)
-- ---------------------------------------------------------------------------
INSERT INTO agent (id, api_login, api_psw, name, allowlistip) VALUES
('agent-demo-01', 'demo_agent', 'AgentApi2026!', 'Demo Payment Agent (MBANK)', NULL),
('agent-demo-02', 'optima_agent', 'OptimaApi2026!', 'Optima Bank Connector', '127.0.0.1;::1');

-- ---------------------------------------------------------------------------
-- 2. Commission + tiers
-- ---------------------------------------------------------------------------
INSERT INTO commission (id, name, commission_kind, rate, fixed_amount, min_fee, max_fee) VALUES
('comm-percent-2',  'Комиссия 2%',           'percent',      0.020000, NULL,   NULL,  NULL),
('comm-percent-15', 'Комиссия 1.5%',         'percent',      0.015000, NULL,   NULL,  NULL),
('comm-fixed-5',    'Фикс 5 сом',            'fixed',        NULL,     500.00, NULL,  NULL),
('comm-mixed',      'Смешанная 1% + 3 сом',  'mixed',        0.010000, 300.00, 100.00, 5000.00),
('comm-progressive','Прогрессивная шкала',   'progressive',  NULL,     NULL,   NULL,  NULL);

INSERT INTO commission_tier (id, commission_id, amount_from, amount_to, rate, sort_order) VALUES
('tier-01', 'comm-progressive', 0,         100000,  0.025000, 1),
('tier-02', 'comm-progressive', 100000,    500000,  0.020000, 2),
('tier-03', 'comm-progressive', 500000,    999999999, 0.015000, 3);

-- ---------------------------------------------------------------------------
-- 3. Permissions (глобальный каталог)
-- ---------------------------------------------------------------------------
INSERT INTO permissions (id, name, code, description, category, isdeleted, area) VALUES
-- Общие / навигация
('perm-01', 'Просмотр дашборда',              'dashboard.view',              'Доступ к главному экрану', 'Навигация', 0, NULL),
('perm-02', 'Навигация: дашборд',             'nav.dashboard',               'Пункт меню Дашборд', 'Навигация', 0, NULL),
('perm-03', 'Навигация: главная',             'nav.home',                    'Пункт меню Главная', 'Навигация', 0, NULL),
('perm-04', 'Навигация: дети/клиенты',        'nav.children',                'Пункт меню Клиенты/Дети', 'Навигация', 0, NULL),
('perm-05', 'Навигация: услуги',              'nav.services',                'Пункт меню Услуги', 'Навигация', 0, NULL),
('perm-06', 'Навигация: группы',              'nav.groups',                  'Пункт меню Группы', 'Навигация', 0, 'detsad'),
('perm-07', 'Навигация: счета',               'nav.invoices',                'Пункт меню Счета', 'Навигация', 0, NULL),
('perm-08', 'Навигация: транзакции',          'nav.transactions',            'Пункт меню Транзакции', 'Навигация', 0, NULL),
('perm-09', 'Навигация: настройки',           'nav.settings',                'Пункт меню Настройки', 'Навигация', 0, NULL),
('perm-10', 'Навигация: поддержка',           'nav.support',                 'Пункт меню Поддержка', 'Навигация', 0, NULL),
('perm-11', 'Навигация: профиль',             'nav.profile',                 'Пункт меню Профиль', 'Навигация', 0, NULL),
('perm-12', 'Навигация: уведомления',         'nav.notifications',           'Пункт меню Уведомления', 'Навигация', 0, NULL),
('perm-13', 'Навигация: приёмы',              'nav.appointments',            'Пункт меню Приёмы', 'Навигация', 0, 'medclinic'),
('perm-14', 'Навигация: врачи',               'nav.doctors',                 'Пункт меню Врачи', 'Навигация', 0, 'medclinic'),
('perm-15', 'Навигация: отделения',           'nav.departments',             'Пункт меню Отделения', 'Навигация', 0, 'medclinic'),
('perm-16', 'Навигация: специализации',       'nav.specializations',         'Пункт меню Специализации', 'Навигация', 0, 'medclinic'),
('perm-17', 'Навигация: пациенты',            'nav.patients',                'Пункт меню Пациенты', 'Навигация', 0, 'medclinic'),
-- Клиенты / дети
('perm-18', 'Просмотр клиентов',              'children.view',               'Просмотр списка клиентов', 'Клиенты', 0, NULL),
('perm-19', 'Создание клиентов',              'children.create',             'Создание клиентов и связанных сущностей', 'Клиенты', 0, NULL),
('perm-20', 'Редактирование клиентов',        'children.edit',               'Редактирование клиентов', 'Клиенты', 0, NULL),
-- Счета / платежи
('perm-21', 'Просмотр счетов',                'invoices.view',               'Просмотр счетов', 'Счета', 0, NULL),
('perm-22', 'Создание счетов',                'invoices.create',             'Создание счетов', 'Счета', 0, NULL),
('perm-23', 'Просмотр транзакций',            'transactions.view',           'Просмотр транзакций и истории оплат', 'Платежи', 0, NULL),
('perm-24', 'Создание платежей',              'payments.create',             'Ручное создание платежей', 'Платежи', 0, NULL),
-- Настройки
('perm-25', 'Просмотр настроек',              'settings.view',               'Доступ к настройкам', 'Настройки', 0, NULL),
('perm-26', 'Настройка агентов',              'settings.agents',             'Управление агентами', 'Настройки', 0, NULL),
('perm-27', 'Тарифы и комиссии',              'settings.tariff',             'Тарифы и комиссии', 'Настройки', 0, NULL),
-- Профиль / поддержка / уведомления
('perm-28', 'Просмотр профиля',               'profile.view',                'Просмотр профиля', 'Профиль', 0, NULL),
('perm-29', 'Поддержка',                      'support.view',                'Доступ к поддержке', 'Поддержка', 0, NULL),
('perm-30', 'Уведомления',                    'notifications.view',          'Просмотр уведомлений', 'Уведомления', 0, NULL),
('perm-49', 'Отправка уведомлений',           'notifications.send',          'Ручная рассылка уведомлений клиентам', 'Уведомления', 0, NULL),
-- Medclinic
('perm-31', 'Просмотр приёмов',               'appointments.view',           'Просмотр календаря приёмов', 'Записи', 0, 'medclinic'),
('perm-32', 'Расписание врача',               'appointments.doctor.view',    'Кабинет врача: своё расписание', 'Записи', 0, 'medclinic'),
('perm-33', 'Регистратура',                   'appointments.registry.view',  'Регистратура: все приёмы', 'Записи', 0, 'medclinic'),
('perm-34', 'Редактирование приёмов',         'appointments.edit',           'Создание и редактирование приёмов', 'Записи', 0, 'medclinic'),
('perm-35', 'Управление приёмами',            'appointments.manage',         'Полное управление приёмами', 'Записи', 0, 'medclinic'),
('perm-36', 'Счёт из приёма',                 'appointments.invoice',        'Формирование счёта из приёма', 'Записи', 0, 'medclinic'),
('perm-37', 'Статус оплаты приёма',           'appointments.payment.status', 'Изменение статуса оплаты приёма', 'Записи', 0, 'medclinic'),
('perm-38', 'Просмотр врачей',                'doctors.view',                'Справочник врачей', 'Медструктура', 0, 'medclinic'),
('perm-48', 'Редактирование сотрудников',     'doctors.edit',                'Создание и изменение карточек сотрудников', 'Медструктура', 0, 'medclinic'),
('perm-39', 'Просмотр отделений',             'departments.view',            'Справочник отделений', 'Медструктура', 0, 'medclinic'),
('perm-40', 'Просмотр специализаций',         'specializations.view',        'Справочник специализаций', 'Медструктура', 0, 'medclinic'),
('perm-41', 'Просмотр пациентов',             'patients.view',               'Справочник пациентов', 'Медструктура', 0, 'medclinic'),
('perm-42', 'Просмотр услуг (мед)',           'services.view',               'Услуги клиники', 'Услуги', 0, 'medclinic'),
('perm-43', 'Услуги организации',             'orgservices.view',            'Услуги организации', 'Услуги', 0, NULL),
('perm-44', 'Суммы: QR SECORE',               'dashboard.sums.qr_secore',    'Отображать суммы, принятые через QR SECORE', 'Дашборд', 0, NULL),
('perm-45', 'Суммы: QR внешний',              'dashboard.sums.qr_external',  'Отображать суммы, принятые через внешний QR', 'Дашборд', 0, NULL),
('perm-46', 'Суммы: наличные',                'dashboard.sums.cash',         'Отображать суммы, принятые через наличные', 'Дашборд', 0, NULL),
('perm-47', 'Суммы: карта',                   'dashboard.sums.card',         'Отображать суммы, принятые через карту', 'Дашборд', 0, NULL);

-- ---------------------------------------------------------------------------
-- 4. Organizations
-- ---------------------------------------------------------------------------
INSERT INTO organization (id, name, organizationtype, is_active) VALUES
('org-med-01',    'Медицинский центр «Здоровье Плюс»', 'medclinic', true),
('01000', 'Детский сад №15 «Балажан»',         'detsad',    true),
('org-school-01', 'Школа №42 им. Абая',                'standart',  true),
('org-simple-01', 'ИП «Касымов Сервис»',               'simple',    true);

-- ---------------------------------------------------------------------------
-- 5. Organization settings
-- ---------------------------------------------------------------------------
INSERT INTO organization_settings (
    organization_id, disable_invoice_service_selection, allowed_hassameaccount,
    invoice_pay_code_mode, paymentreminderdaysbefore, email, whatsapp_phone,
    contact_phone, director_full_name, address, logo_path, billing_type,
    default_qr_mode, use_lower_commission_from_org, commission_id,
    use_upper_commission_from_agent, use_lower_commission_to_agent
) VALUES
('org-med-01', false, false, 'both', 3,
 'info@zdorovie.kg', '+996700111001', '+996312111001', 'Асанова Гульнара Токтогуловна',
 'г. Бишкек, ул. Ибраимова 115', NULL, 'subscription', 'reuse_active',
 false, NULL, false, false),
('01000', false, true, 'both', 5,
 'info@balazhan.kg', '+996700222002', '+996312222002', 'Бекова Айгуль Сапарбековна',
 'г. Бишкек, мкр. Джал-23, д. 7', NULL, NULL, 'reuse_active',
 true, 'comm-percent-2', true, true),
('org-school-01', false, false, 'new_only', 7,
 'office@school42.kg', '+996700333003', '+996312333003', 'Жумабаев Нурлан Асылбекович',
 'г. Бишкек, ул. Московская 78', NULL, NULL, 'regenerate_per_period',
 true, 'comm-percent-15', true, false),
('org-simple-01', true, false, 'new_only', 2,
 'kasymov@mail.kg', '+996700444004', '+996555444004', 'Касымов Эрлан Болотбекович',
 'г. Ош, ул. Ленина 45', NULL, 'subscription', 'reuse_active',
 false, NULL, false, false);

-- ---------------------------------------------------------------------------
-- 6. Subscriptions
-- ---------------------------------------------------------------------------
INSERT INTO organization_subscription (id, organization_id, price_tyiyn, period_type, created_at, updated_at) VALUES
('sub-med-01',    'org-med-01',    1500000.00, 'month', NOW(), NOW()),
('sub-simple-01', 'org-simple-01',  500000.00, 'month', NOW(), NOW());

INSERT INTO organization_subscription_payment (
    id, organization_id, paid_at, amount_tyiyn, period_start, period_end, note, created_at
) VALUES
('subpay-med-01', 'org-med-01', NOW() - INTERVAL '20 days', 1500000.00,
 (CURRENT_DATE - INTERVAL '20 days')::date, (CURRENT_DATE + INTERVAL '10 days')::date,
 'Оплата подписки за текущий месяц', NOW() - INTERVAL '20 days'),
('subpay-med-00', 'org-med-01', NOW() - INTERVAL '50 days', 1500000.00,
 (CURRENT_DATE - INTERVAL '50 days')::date, (CURRENT_DATE - INTERVAL '21 days')::date,
 'Оплата подписки за прошлый месяц', NOW() - INTERVAL '50 days'),
('subpay-simple-01', 'org-simple-01', NOW() - INTERVAL '5 days', 500000.00,
 (CURRENT_DATE - INTERVAL '5 days')::date, (CURRENT_DATE + INTERVAL '25 days')::date,
 'Ежемесячная подписка', NOW() - INTERVAL '5 days');

-- ---------------------------------------------------------------------------
-- 7. Roles (per organization)
-- ---------------------------------------------------------------------------
INSERT INTO roles (id, name, organization, isdeleted, rights, avilable_services) VALUES
-- Medclinic
('role-med-admin',    'Администратор', 'org-med-01', 0, NULL, NULL),
('role-med-doctor',   'Врач',          'org-med-01', 0, NULL, NULL),
('role-med-registry', 'Регистратор',   'org-med-01', 0, NULL, NULL),
-- Detsad
('role-ds-admin',     'Администратор', '01000', 0, NULL, NULL),
('role-ds-teacher',   'Воспитатель',   '01000', 0, NULL, NULL),
-- School (standart)
('role-sch-admin',    'Администратор', 'org-school-01', 0, NULL, NULL),
('role-sch-acc',      'Бухгалтер',     'org-school-01', 0, NULL, NULL),
-- Simple
('role-sm-admin',     'Администратор', 'org-simple-01', 0, NULL, NULL);

-- ---------------------------------------------------------------------------
-- 8. Users (password = Demo123!)
-- ---------------------------------------------------------------------------
INSERT INTO users (
    id, name, email, phone, password, isdeleted, organization, role, start_page, email_confirmed
) VALUES
('user-med-admin', 'Асанова Гульнара Токтогуловна', 'admin@zdorovie.kg', '+996700111101',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-med-01', 'admin', 'cabinet', true),
('user-med-doc1', 'Исаев Алмаз Бектурович', 'doctor@zdorovie.kg', '+996700111102',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-med-01', 'user', 'appointments', true),
('user-med-doc2', 'Сулейманова Динара Омурбековна', 'doctor2@zdorovie.kg', '+996700111103',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-med-01', 'user', 'appointments', true),
('user-med-reg', 'Мамбетова Жазгуль Нурлановна', 'registry@zdorovie.kg', '+996700111104',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-med-01', 'user', 'appointments', true),

('user-ds-admin', 'Бекова Айгуль Сапарбековна', 'admin@balazhan.kg', '+996700222201',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, '01000', 'admin', 'cabinet', true),
('user-ds-teacher', 'Омурзакова Мээрим Айбековна', 'teacher@balazhan.kg', '+996700222202',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, '01000', 'user', 'cabinet', true),

('user-sch-admin', 'Жумабаев Нурлан Асылбекович', 'admin@school42.kg', '+996700333301',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-school-01', 'admin', 'cabinet', true),
('user-sch-acc', 'Турдубекова Айнура Калыбековна', 'accountant@school42.kg', '+996700333302',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-school-01', 'user', 'cabinet', true),

('user-sm-admin', 'Касымов Эрлан Болотбекович', 'admin@kasymov.kg', '+996700444401',
 'WIxV884rhWmxU8WrvxP590MIuIogAXzGmbg1zJMZXRY=', 0, 'org-simple-01', 'admin', 'cabinet', true);

-- ---------------------------------------------------------------------------
-- 9. user_roles
-- ---------------------------------------------------------------------------
INSERT INTO user_roles (id, "user", role, isdeleted) VALUES
('ur-med-admin', 'user-med-admin', 'role-med-admin', 0),
('ur-med-doc1',  'user-med-doc1',  'role-med-doctor', 0),
('ur-med-doc2',  'user-med-doc2',  'role-med-doctor', 0),
('ur-med-reg',   'user-med-reg',   'role-med-registry', 0),
('ur-ds-admin',  'user-ds-admin',  'role-ds-admin', 0),
('ur-ds-teacher','user-ds-teacher','role-ds-teacher', 0),
('ur-sch-admin', 'user-sch-admin', 'role-sch-admin', 0),
('ur-sch-acc',   'user-sch-acc',   'role-sch-acc', 0),
('ur-sm-admin',  'user-sm-admin',  'role-sm-admin', 0);

-- ---------------------------------------------------------------------------
-- 10. role_permissions
--    Админы — почти все права своего профиля
--    Врач / регистратор / воспитатель / бухгалтер — урезанные наборы
-- ---------------------------------------------------------------------------

-- Medclinic admin: все права (глобальные + medclinic)
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-med-admin-' || p.id, 'role-med-admin', p.id, 0
FROM permissions p
WHERE p.isdeleted = 0
  AND (p.area IS NULL OR p.area = 'medclinic');

-- Medclinic doctor
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-med-doc-' || p.id, 'role-med-doctor', p.id, 0
FROM permissions p
WHERE p.code IN (
    'dashboard.view', 'nav.dashboard', 'nav.home',
    'appointments.view', 'appointments.doctor.view', 'appointments.edit',
    'appointments.invoice', 'appointments.payment.status',
    'nav.appointments', 'patients.view', 'nav.patients',
    'invoices.view', 'nav.invoices', 'transactions.view',
    'profile.view', 'nav.profile', 'notifications.view', 'nav.notifications'
);

-- Medclinic registry
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-med-reg-' || p.id, 'role-med-registry', p.id, 0
FROM permissions p
WHERE p.code IN (
    'dashboard.view', 'nav.dashboard', 'nav.home',
    'dashboard.sums.qr_secore', 'dashboard.sums.qr_external', 'dashboard.sums.cash', 'dashboard.sums.card',
    'appointments.view', 'appointments.registry.view', 'appointments.edit',
    'appointments.manage', 'appointments.invoice', 'appointments.payment.status',
    'nav.appointments', 'doctors.view', 'nav.doctors',
    'patients.view', 'nav.patients', 'departments.view', 'nav.departments',
    'specializations.view', 'nav.specializations',
    'services.view', 'orgservices.view', 'nav.services',
    'invoices.view', 'invoices.create', 'nav.invoices',
    'transactions.view', 'nav.transactions', 'payments.create',
    'notifications.view', 'nav.notifications',
    'profile.view', 'nav.profile', 'settings.view', 'nav.settings'
);

-- Detsad admin
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-ds-admin-' || p.id, 'role-ds-admin', p.id, 0
FROM permissions p
WHERE p.isdeleted = 0
  AND (p.area IS NULL OR p.area = 'detsad')
  AND p.area IS DISTINCT FROM 'medclinic';

-- Detsad teacher (просмотр детей/групп)
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-ds-tch-' || p.id, 'role-ds-teacher', p.id, 0
FROM permissions p
WHERE p.code IN (
    'dashboard.view', 'nav.dashboard', 'nav.home',
    'nav.children', 'children.view',
    'nav.groups', 'nav.services', 'children.view',
    'nav.invoices', 'invoices.view',
    'profile.view', 'nav.profile', 'notifications.view', 'nav.notifications'
);

-- School admin
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-sch-admin-' || p.id, 'role-sch-admin', p.id, 0
FROM permissions p
WHERE p.isdeleted = 0
  AND (p.area IS NULL OR p.area = 'standart')
  AND COALESCE(p.area, '') NOT IN ('medclinic', 'detsad');

-- School accountant
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-sch-acc-' || p.id, 'role-sch-acc', p.id, 0
FROM permissions p
WHERE p.code IN (
    'dashboard.view', 'nav.dashboard', 'nav.home',
    'nav.children', 'children.view', 'children.create', 'children.edit',
    'nav.services', 'children.view',
    'invoices.view', 'invoices.create', 'nav.invoices',
    'transactions.view', 'nav.transactions', 'payments.create',
    'profile.view', 'nav.profile', 'notifications.view', 'nav.notifications'
);

-- Simple admin
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-sm-admin-' || p.id, 'role-sm-admin', p.id, 0
FROM permissions p
WHERE p.code IN (
    'dashboard.view', 'nav.dashboard', 'nav.home',
    'invoices.view', 'invoices.create', 'nav.invoices',
    'transactions.view', 'nav.transactions', 'payments.create',
    'settings.view', 'nav.settings', 'settings.agents', 'settings.tariff',
    'notifications.view', 'nav.notifications',
    'profile.view', 'nav.profile', 'support.view', 'nav.support'
);

-- ---------------------------------------------------------------------------
-- 11. Departments + specializations (medclinic)
-- ---------------------------------------------------------------------------
INSERT INTO departments (id, organization_id, name, code, description, is_active, sort_order, created_at, updated_at) VALUES
('dep-med-therapy',  'org-med-01', 'Терапевтическое отделение', 'THERAPY',  'Общая терапия и семейная медицина', true, 1, NOW(), NOW()),
('dep-med-cardio',   'org-med-01', 'Кардиология',               'CARDIO',   'Диагностика и лечение ССС', true, 2, NOW(), NOW()),
('dep-med-ped',      'org-med-01', 'Педиатрия',                 'PED',      'Детская поликлиника', true, 3, NOW(), NOW()),
('dep-med-lab',      'org-med-01', 'Лаборатория',               'LAB',      'Клинические анализы', true, 4, NOW(), NOW());

INSERT INTO specializations (id, organization_id, department_id, name, description, is_active, sort_order, created_at, updated_at) VALUES
('spec-med-therapist', 'org-med-01', 'dep-med-therapy', 'Терапевт',           'Врач общей практики', true, 1, NOW(), NOW()),
('spec-med-cardio',    'org-med-01', 'dep-med-cardio',  'Кардиолог',          'Специалист по ССС', true, 2, NOW(), NOW()),
('spec-med-ped',       'org-med-01', 'dep-med-ped',     'Педиатр',            'Детский врач', true, 3, NOW(), NOW()),
('spec-med-echo',      'org-med-01', 'dep-med-cardio',  'УЗИ-кардиолог',      'Эхокардиография', true, 4, NOW(), NOW()),
('spec-med-lab',       'org-med-01', 'dep-med-lab',     'Лаборант-врач',      'Клиническая лаборатория', true, 5, NOW(), NOW());

INSERT INTO user_departments (id, user_id, department_id, is_primary, created_at) VALUES
('ud-doc1-therapy', 'user-med-doc1', 'dep-med-therapy', true,  NOW()),
('ud-doc1-cardio',  'user-med-doc1', 'dep-med-cardio',  false, NOW()),
('ud-doc2-ped',     'user-med-doc2', 'dep-med-ped',     true,  NOW());

INSERT INTO user_specializations (id, user_id, specialization_id, is_primary, created_at) VALUES
('us-doc1-ther', 'user-med-doc1', 'spec-med-therapist', true,  NOW()),
('us-doc1-card', 'user-med-doc1', 'spec-med-cardio',    false, NOW()),
('us-doc2-ped',  'user-med-doc2', 'spec-med-ped',       true,  NOW());

-- ---------------------------------------------------------------------------
-- 12. Organization services
-- ---------------------------------------------------------------------------
INSERT INTO organization_services (id, name, organization, service_summ, fixed_sum, min_summ, max_summ, isdeleted) VALUES
-- Medclinic
('svc-med-consult',  'Первичный приём терапевта',     'org-med-01', 150000, 1, NULL, NULL, 0),
('svc-med-cardio',   'Консультация кардиолога',       'org-med-01', 250000, 1, NULL, NULL, 0),
('svc-med-echo',     'ЭхоКГ',                         'org-med-01', 350000, 1, NULL, NULL, 0),
('svc-med-ped',      'Приём педиатра',                'org-med-01', 120000, 1, NULL, NULL, 0),
('svc-med-blood',    'Общий анализ крови',            'org-med-01',  45000, 1, NULL, NULL, 0),
('svc-med-package',  'Чек-ап «Базовый»',              'org-med-01', 890000, 1, NULL, NULL, 0),
-- Detsad
('svc-ds-fee',       'Ежемесячная оплата за сад',     '01000', 850000, 1, NULL, NULL, 0),
('svc-ds-food',      'Питание',                       '01000', 250000, 1, NULL, NULL, 0),
('svc-ds-extra',     'Кружки / доп.занятия',          '01000', 150000, 0, 50000, 500000, 0),
('svc-ds-camp',      'Летний лагерь (1 смена)',       '01000',1200000, 1, NULL, NULL, 0),
-- School
('svc-sch-tuition',  'Обучение (месяц)',              'org-school-01', 550000, 1, NULL, NULL, 0),
('svc-sch-lunch',    'Питание в столовой',            'org-school-01', 180000, 1, NULL, NULL, 0),
('svc-sch-uniform',  'Форма / учебные материалы',     'org-school-01', 320000, 0, 100000, 800000, 0),
('svc-sch-transport','Подвоз (месяц)',                'org-school-01', 200000, 1, NULL, NULL, 0),
-- Simple
('svc-sm-rent',      'Аренда помещения',              'org-simple-01',2500000, 1, NULL, NULL, 0),
('svc-sm-consult',   'Консультация / услуга',         'org-simple-01', 500000, 0, 100000, 5000000, 0),
('svc-sm-delivery',  'Доставка',                      'org-simple-01',  80000, 1, NULL, NULL, 0);

INSERT INTO service_specializations (id, organization_service_id, specialization_id, created_at) VALUES
('ss-consult-ther', 'svc-med-consult', 'spec-med-therapist', NOW()),
('ss-cardio-card',  'svc-med-cardio',  'spec-med-cardio',    NOW()),
('ss-echo-echo',    'svc-med-echo',    'spec-med-echo',      NOW()),
('ss-ped-ped',      'svc-med-ped',     'spec-med-ped',       NOW()),
('ss-blood-lab',    'svc-med-blood',   'spec-med-lab',       NOW());

-- ---------------------------------------------------------------------------
-- 13. Client groups (detsad + school)
-- ---------------------------------------------------------------------------
INSERT INTO org_client_groups (id, name, parent_group_id, organization_id, is_deleted, logo, created_date) VALUES
('grp-ds-root',   'Все группы',   NULL,          '01000', 0, NULL, NOW()),
('grp-ds-junior', 'Младшая (2-3)', 'grp-ds-root', '01000', 0, NULL, NOW()),
('grp-ds-middle', 'Средняя (3-4)', 'grp-ds-root', '01000', 0, NULL, NOW()),
('grp-ds-senior', 'Старшая (5-6)', 'grp-ds-root', '01000', 0, NULL, NOW()),
('grp-sch-root',  'Классы',        NULL,          'org-school-01', 0, NULL, NOW()),
('grp-sch-5a',    '5 «А» класс',   'grp-sch-root','org-school-01', 0, NULL, NOW()),
('grp-sch-7b',    '7 «Б» класс',   'grp-sch-root','org-school-01', 0, NULL, NOW()),
('grp-sch-9a',    '9 «А» класс',   'grp-sch-root','org-school-01', 0, NULL, NOW());

-- ---------------------------------------------------------------------------
-- 14. Organization fields + clients
-- ---------------------------------------------------------------------------
INSERT INTO organization_fields (id, field_name, field_type, field_select_values, isdeleted, organization, filterbyfield) VALUES
('fld-ds-birth',   'Дата рождения',     'datetime', NULL, 0, '01000', true),
('fld-ds-allergy', 'Аллергия',          'selected', 'Нет;Пищевая;Лекарственная;Другая', 0, '01000', true),
('fld-ds-parent',  'ФИО родителя',      'string',   NULL, 0, '01000', false),
('fld-sch-grade',  'Класс',             'selected', '5А;5Б;7А;7Б;9А;9Б', 0, 'org-school-01', true),
('fld-sch-doc',    'Серия паспорта',    'string',   NULL, 0, 'org-school-01', false),
('fld-med-blood',  'Группа крови',      'selected', 'I(0);II(A);III(B);IV(AB)', 0, 'org-med-01', true),
('fld-med-chronic','Хронические заболевания', 'string', NULL, 0, 'org-med-01', false);

INSERT INTO organization_clients (
    id, organization, client_name, client_type, client_inn, client_phone, client_address,
    client_email, client_balance, client_status, created_date, updated_date, user_creater,
    client_wa, client_tg, org_client_group_id
) VALUES
-- Medclinic patients
('cli-med-01', 'org-med-01', 'Абдыкадыров Тимур Серикович', 'fiz', NULL, '+996555101001',
 'г. Бишкек, ул. Чуй 120, кв. 15', 'timur.abdy@mail.kg', 0, 1, NOW() - INTERVAL '60 days', NOW(), 'user-med-admin',
 '+996555101001', '@timur_ab', NULL),
('cli-med-02', 'org-med-01', 'Кожоева Айжан Маратовна', 'fiz', NULL, '+996555101002',
 'г. Бишкек, мкр. Асанбай 12-34', 'aizhan.k@gmail.com', -150000, 1, NOW() - INTERVAL '45 days', NOW(), 'user-med-admin',
 '+996555101002', NULL, NULL),
('cli-med-03', 'org-med-01', 'ООО «Ак-Сай Трейд»', 'jur', '01234567890123', '+996312101003',
 'г. Бишкек, пр. Манаса 45', 'hr@aksay.kg', 500000, 1, NOW() - INTERVAL '30 days', NOW(), 'user-med-reg',
 NULL, NULL, NULL),
('cli-med-04', 'org-med-01', 'Сыдыков Бекзат Эрмекович', 'fiz', NULL, '+996555101004',
 'г. Бишкек, ул. Киевская 88', 'bekzat.s@mail.ru', 0, 1, NOW() - INTERVAL '10 days', NOW(), 'user-med-reg',
 '+996555101004', '@bekzat', NULL),
('cli-med-05', 'org-med-01', 'Нурматова Самара Асылбековна', 'fiz', NULL, '+996555101005',
 'г. Бишкек, мкр. Джал 15-8', 'samara.n@gmail.com', 0, 0, NOW() - INTERVAL '5 days', NOW(), 'user-med-admin',
 NULL, NULL, NULL),

-- Detsad children (client_name = ребёнок / плательщик)
('cli-ds-01', '01000', 'Алиева Амина (мать: Алиева Нургуль)', 'fiz', NULL, '+996555201001',
 'г. Бишкек, мкр. Джал-23, д. 12', 'nurgul.alieva@mail.kg', -850000, 1, NOW() - INTERVAL '90 days', NOW(), 'user-ds-admin',
 '+996555201001', NULL, 'grp-ds-junior'),
('cli-ds-02', '01000', 'Бекмурзаев Арстан (отец: Бекмурзаев Талант)', 'fiz', NULL, '+996555201002',
 'г. Бишкек, ул. Ахунбаева 45', 'talant.b@gmail.com', 0, 1, NOW() - INTERVAL '80 days', NOW(), 'user-ds-admin',
 '+996555201002', '@talant_b', 'grp-ds-middle'),
('cli-ds-03', '01000', 'Жээнбекова Медина (мать: Жээнбекова Айчурек)', 'fiz', NULL, '+996555201003',
 'г. Бишкек, мкр. Тунгуч 3-21', 'aichurek.j@mail.kg', 250000, 1, NOW() - INTERVAL '70 days', NOW(), 'user-ds-admin',
 '+996555201003', NULL, 'grp-ds-senior'),
('cli-ds-04', '01000', 'Кадыров Дастан (мать: Кадырова Эльвира)', 'fiz', NULL, '+996555201004',
 'г. Бишкек, ул. Байтик Баатыра 10', 'elvira.k@gmail.com', -1100000, 1, NOW() - INTERVAL '40 days', NOW(), 'user-ds-teacher',
 NULL, NULL, 'grp-ds-junior'),
('cli-ds-05', '01000', 'Токтосунова Арууке (отец: Токтосунов Максат)', 'fiz', NULL, '+996555201005',
 'г. Бишкек, мкр. Кок-Жар 7-14', 'maksat.t@mail.kg', 0, 0, NOW() - INTERVAL '15 days', NOW(), 'user-ds-admin',
 '+996555201005', NULL, 'grp-ds-middle'),

-- School students / parents
('cli-sch-01', 'org-school-01', 'Исмаилов Арсен (5А)', 'fiz', NULL, '+996555301001',
 'г. Бишкек, ул. Московская 12', 'parent.ismailov@mail.kg', -550000, 1, NOW() - INTERVAL '100 days', NOW(), 'user-sch-admin',
 '+996555301001', NULL, 'grp-sch-5a'),
('cli-sch-02', 'org-school-01', 'Кыдырбаева Айгерим (5А)', 'fiz', NULL, '+996555301002',
 'г. Бишкек, ул. Советская 34', 'aigerim.parent@gmail.com', 0, 1, NOW() - INTERVAL '95 days', NOW(), 'user-sch-admin',
 NULL, NULL, 'grp-sch-5a'),
('cli-sch-03', 'org-school-01', 'Орозбеков Данияр (7Б)', 'fiz', NULL, '+996555301003',
 'г. Бишкек, пр. Чуй 256', 'daniyar.o@mail.kg', -730000, 1, NOW() - INTERVAL '60 days', NOW(), 'user-sch-acc',
 '+996555301003', NULL, 'grp-sch-7b'),
('cli-sch-04', 'org-school-01', 'Садыкова Милана (9А)', 'fiz', NULL, '+996555301004',
 'г. Бишкек, ул. Исанова 67', 'milana.s@gmail.com', 180000, 1, NOW() - INTERVAL '50 days', NOW(), 'user-sch-admin',
 NULL, NULL, 'grp-sch-9a'),
('cli-sch-05', 'org-school-01', 'ООО «Билим Плюс» (корпоративный)', 'jur', '02345678901234', '+996312301005',
 'г. Бишкек, ул. Тоголок Молдо 1', 'billing@bilim.kg', 0, 1, NOW() - INTERVAL '20 days', NOW(), 'user-sch-acc',
 NULL, NULL, NULL),

-- Simple clients
('cli-sm-01', 'org-simple-01', 'ИП Нурланов А.К.', 'jur', '03456789012345', '+996555401001',
 'г. Ош, ул. Курманжан Датка 22', 'nurlanov@mail.kg', -2500000, 1, NOW() - INTERVAL '40 days', NOW(), 'user-sm-admin',
 '+996555401001', NULL, NULL),
('cli-sm-02', 'org-simple-01', 'Сатыбалдиева Жылдыз', 'fiz', NULL, '+996555401002',
 'г. Ош, ул. Ленина 10', 'j.satybaldi@gmail.com', 0, 1, NOW() - INTERVAL '25 days', NOW(), 'user-sm-admin',
 NULL, NULL, NULL),
('cli-sm-03', 'org-simple-01', 'ОсОО «Юг-Логистик»', 'jur', '04567890123456', '+996322401003',
 'г. Ош, ул. Масалиева 88', 'finance@southlog.kg', 80000, 1, NOW() - INTERVAL '12 days', NOW(), 'user-sm-admin',
 NULL, NULL, NULL);

INSERT INTO organization_clients_additional_fields (id, field, organization_client, value) VALUES
('af-ds-01-birth', 'fld-ds-birth',   'cli-ds-01', '2023-03-15'),
('af-ds-01-all',   'fld-ds-allergy', 'cli-ds-01', 'Пищевая'),
('af-ds-01-par',   'fld-ds-parent',  'cli-ds-01', 'Алиева Нургуль Сериковна'),
('af-ds-02-birth', 'fld-ds-birth',   'cli-ds-02', '2022-07-22'),
('af-ds-02-all',   'fld-ds-allergy', 'cli-ds-02', 'Нет'),
('af-ds-02-par',   'fld-ds-parent',  'cli-ds-02', 'Бекмурзаев Талант Жолдошевич'),
('af-sch-01-gr',   'fld-sch-grade',  'cli-sch-01', '5А'),
('af-sch-03-gr',   'fld-sch-grade',  'cli-sch-03', '7Б'),
('af-med-01-bl',   'fld-med-blood',  'cli-med-01', 'II(A)'),
('af-med-02-bl',   'fld-med-blood',  'cli-med-02', 'I(0)'),
('af-med-02-ch',   'fld-med-chronic','cli-med-02', 'Гипертония');

-- ---------------------------------------------------------------------------
-- 15. Appointment settings, schedules, templates (medclinic)
-- ---------------------------------------------------------------------------
INSERT INTO appointment_settings (id, organization_id, user_id, appointment_duration_minutes, created_at, updated_at) VALUES
('aps-doc1', 'org-med-01', 'user-med-doc1', 30, NOW(), NOW()),
('aps-doc2', 'org-med-01', 'user-med-doc2', 20, NOW(), NOW());

-- day_of_week: 0=Sunday ... 6=Saturday (.NET DayOfWeek)
INSERT INTO user_work_schedules (id, organization_id, user_id, day_of_week, start_time, end_time, is_active, created_at, updated_at) VALUES
('ws-doc1-1', 'org-med-01', 'user-med-doc1', 1, '09:00', '13:00', true, NOW(), NOW()),
('ws-doc1-2', 'org-med-01', 'user-med-doc1', 2, '09:00', '13:00', true, NOW(), NOW()),
('ws-doc1-3', 'org-med-01', 'user-med-doc1', 3, '14:00', '18:00', true, NOW(), NOW()),
('ws-doc1-4', 'org-med-01', 'user-med-doc1', 4, '09:00', '13:00', true, NOW(), NOW()),
('ws-doc1-5', 'org-med-01', 'user-med-doc1', 5, '09:00', '13:00', true, NOW(), NOW()),
('ws-doc2-1', 'org-med-01', 'user-med-doc2', 1, '10:00', '16:00', true, NOW(), NOW()),
('ws-doc2-2', 'org-med-01', 'user-med-doc2', 2, '10:00', '16:00', true, NOW(), NOW()),
('ws-doc2-3', 'org-med-01', 'user-med-doc2', 4, '10:00', '16:00', true, NOW(), NOW()),
('ws-doc2-4', 'org-med-01', 'user-med-doc2', 5, '10:00', '14:00', true, NOW(), NOW());

INSERT INTO user_work_schedule_overrides (id, organization_id, user_id, work_date, is_working, start_time, end_time, comment, created_at, updated_at) VALUES
('wso-doc1-1', 'org-med-01', 'user-med-doc1', (CURRENT_DATE + INTERVAL '3 days')::date, false, NULL, NULL, 'Отпуск / выходной', NOW(), NOW()),
('wso-doc2-1', 'org-med-01', 'user-med-doc2', (CURRENT_DATE + INTERVAL '1 day')::date, true, '11:00', '15:00', 'Сокращённый день', NOW(), NOW());

INSERT INTO appointment_medical_templates (id, organization_id, template_type, title, content, sort_order, is_active, created_at, updated_at) VALUES
('tpl-med-complaints', 'org-med-01', 'complaints', 'Типовые жалобы (терапия)',
 'Головная боль, слабость, повышение АД. Длительность симптомов: ____ дней.', 1, true, NOW(), NOW()),
('tpl-med-diagnosis',  'org-med-01', 'diagnosis',  'Гипертоническая болезнь',
 'Гипертоническая болезнь II ст., риск 2. Рекомендован контроль АД.', 1, true, NOW(), NOW()),
('tpl-med-recom',      'org-med-01', 'recommendations', 'Общие рекомендации',
 'Диета с ограничением соли, дозированная физ.нагрузка, контроль АД 2 р/день.', 1, true, NOW(), NOW()),
('tpl-med-research',   'org-med-01', 'research', 'ОАК + биохимия',
 'Общий анализ крови, глюкоза, липидный профиль, креатинин.', 1, true, NOW(), NOW()),
('tpl-med-referral',   'org-med-01', 'referral', 'Направление к кардиологу',
 'Направлен к кардиологу для ЭхоКГ и коррекции терапии.', 1, true, NOW(), NOW());

-- ---------------------------------------------------------------------------
-- 16. Appointments + appointment_services
-- ---------------------------------------------------------------------------
INSERT INTO appointments (
    id, organization_id, patient_id, doctor_id, title, phone, email, notes,
    referral_source, payment_type, appointment_status, is_active,
    starts_at, ends_at, created_at, updated_at, created_by
) VALUES
('apt-01', 'org-med-01', 'cli-med-01', 'user-med-doc1', 'Приём терапевта', '+996555101001', 'timur.abdy@mail.kg',
 'Первичный осмотр, жалобы на головную боль', 'самообращение', 'invoice_paid', 'active', true,
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '09:30',
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '10:00',
 NOW() - INTERVAL '2 days', NOW(), 'user-med-reg'),
('apt-02', 'org-med-01', 'cli-med-02', 'user-med-doc1', 'Консультация кардиолога', '+996555101002', 'aizhan.k@gmail.com',
 'Контроль АД, коррекция терапии', 'повторный', 'unpaid', 'active', true,
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '10:30',
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '11:00',
 NOW() - INTERVAL '1 day', NOW(), 'user-med-reg'),
('apt-03', 'org-med-01', 'cli-med-04', 'user-med-doc2', 'Приём педиатра', '+996555101004', 'bekzat.s@mail.ru',
 'Ребёнок 5 лет, плановый осмотр', 'рекомендация', 'none', 'active', true,
 date_trunc('day', NOW()) + INTERVAL '2 days' + TIME '11:00',
 date_trunc('day', NOW()) + INTERVAL '2 days' + TIME '11:20',
 NOW(), NOW(), 'user-med-reg'),
('apt-04', 'org-med-01', 'cli-med-01', 'user-med-doc1', 'ЭхоКГ', '+996555101001', 'timur.abdy@mail.kg',
 'По направлению терапевта', 'направление', 'invoice_paid', 'active', true,
 date_trunc('day', NOW()) - INTERVAL '3 days' + TIME '14:00',
 date_trunc('day', NOW()) - INTERVAL '3 days' + TIME '14:30',
 NOW() - INTERVAL '5 days', NOW() - INTERVAL '3 days', 'user-med-admin'),
('apt-05', 'org-med-01', 'cli-med-03', 'user-med-doc1', 'Корпоративный чек-ап', '+996312101003', 'hr@aksay.kg',
 'Сотрудник ООО Ак-Сай', 'корпоративный', 'unpaid', 'draft', true,
 date_trunc('day', NOW()) + INTERVAL '5 days' + TIME '09:00',
 date_trunc('day', NOW()) + INTERVAL '5 days' + TIME '10:00',
 NOW(), NOW(), 'user-med-admin'),
('apt-06', 'org-med-01', 'cli-med-05', 'user-med-doc2', 'Отменённый приём', '+996555101005', NULL,
 'Пациент не явился', 'самообращение', 'unpaid', 'cancelled', false,
 date_trunc('day', NOW()) - INTERVAL '1 day' + TIME '12:00',
 date_trunc('day', NOW()) - INTERVAL '1 day' + TIME '12:20',
 NOW() - INTERVAL '3 days', NOW() - INTERVAL '1 day', 'user-med-reg');

INSERT INTO appointment_services (id, appointment_id, organization_service_id, service_name, price_tyiyn, quantity) VALUES
('as-01', 'apt-01', 'svc-med-consult', 'Первичный приём терапевта', 150000, 1),
('as-02', 'apt-02', 'svc-med-cardio',  'Консультация кардиолога',   250000, 1),
('as-03', 'apt-03', 'svc-med-ped',     'Приём педиатра',            120000, 1),
('as-04', 'apt-04', 'svc-med-echo',    'ЭхоКГ',                     350000, 1),
('as-05', 'apt-04', 'svc-med-blood',   'Общий анализ крови',         45000, 1),
('as-06', 'apt-05', 'svc-med-package', 'Чек-ап «Базовый»',          890000, 1);

-- ---------------------------------------------------------------------------
-- 17. Invoices + payments + services + QR
-- ---------------------------------------------------------------------------
INSERT INTO invoice (
    id, auto_prolongation, balance, client, date_created, date_start_invoice, date_end_invoice,
    fixed_summ, hassameaccount, invoice_status, name_invoice, next_start_invoice,
    pay_code, periodicity, qr_mode, user_creater
) VALUES
-- Medclinic
('inv-med-01', false, 0, 'cli-med-01', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', NOW() + INTERVAL '20 days',
 150000, false, 'actual', 'Приём терапевта — Абдыкадыров Т.', NULL, 'MED-10001', 'oneTime', 'reuse_active', 'user-med-admin'),
('inv-med-02', false, -250000, 'cli-med-02', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days', NOW() + INTERVAL '25 days',
 250000, false, 'actual', 'Кардиолог — Кожоева А.', NULL, 'MED-10002', 'oneTime', 'reuse_active', 'user-med-reg'),
('inv-med-03', false, 0, 'cli-med-01', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', NOW() + INTERVAL '26 days',
 395000, false, 'closed', 'ЭхоКГ + ОАК — Абдыкадыров Т.', NULL, 'MED-10003', 'oneTime', 'reuse_active', 'user-med-admin'),
('inv-med-04', false, -890000, 'cli-med-03', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', NOW() + INTERVAL '29 days',
 890000, false, 'actual', 'Чек-ап Базовый — Ак-Сай', NULL, 'MED-10004', 'oneTime', 'reuse_active', 'user-med-admin'),

-- Detsad (monthly)
('inv-ds-01', true, -850000, 'cli-ds-01', NOW() - INTERVAL '25 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 850000, false, 'actual', 'Оплата за сад — Алиева Амина', date_trunc('month', NOW()) + INTERVAL '1 month', 'DS-20001', 'monthly', 'reuse_active', 'user-ds-admin'),
('inv-ds-02', true, 0, 'cli-ds-02', NOW() - INTERVAL '20 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 1100000, false, 'actual', 'Сад + питание — Бекмурзаев Арстан', date_trunc('month', NOW()) + INTERVAL '1 month', 'DS-20002', 'monthly', 'reuse_active', 'user-ds-admin'),
('inv-ds-03', true, 250000, 'cli-ds-03', NOW() - INTERVAL '15 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 1000000, false, 'actual', 'Сад + кружки — Жээнбекова Медина', date_trunc('month', NOW()) + INTERVAL '1 month', 'DS-20003', 'monthly', 'reuse_active', 'user-ds-admin'),
('inv-ds-04', true, -1100000, 'cli-ds-04', NOW() - INTERVAL '12 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 1100000, false, 'actual', 'Сад + питание — Кадыров Дастан', date_trunc('month', NOW()) + INTERVAL '1 month', 'DS-20004', 'monthly', 'reuse_active', 'user-ds-admin'),
('inv-ds-05', false, 0, 'cli-ds-02', NOW() - INTERVAL '100 days', NOW() - INTERVAL '100 days', NOW() - INTERVAL '70 days',
 1200000, false, 'closed', 'Летний лагерь — Бекмурзаев Арстан', NULL, 'DS-20005', 'oneTime', 'reuse_active', 'user-ds-admin'),

-- School
('inv-sch-01', true, -550000, 'cli-sch-01', NOW() - INTERVAL '18 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 550000, false, 'actual', 'Обучение — Исмаилов Арсен', date_trunc('month', NOW()) + INTERVAL '1 month', 'SCH-30001', 'monthly', 'regenerate_per_period', 'user-sch-admin'),
('inv-sch-02', true, 0, 'cli-sch-02', NOW() - INTERVAL '18 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 730000, false, 'actual', 'Обучение + питание — Кыдырбаева А.', date_trunc('month', NOW()) + INTERVAL '1 month', 'SCH-30002', 'monthly', 'regenerate_per_period', 'user-sch-admin'),
('inv-sch-03', true, -730000, 'cli-sch-03', NOW() - INTERVAL '10 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 730000, false, 'actual', 'Обучение + питание — Орозбеков Д.', date_trunc('month', NOW()) + INTERVAL '1 month', 'SCH-30003', 'monthly', 'regenerate_per_period', 'user-sch-acc'),
('inv-sch-04', false, 0, 'cli-sch-04', NOW() - INTERVAL '40 days', NOW() - INTERVAL '40 days', NOW() + INTERVAL '50 days',
 320000, false, 'closed', 'Форма — Садыкова Милана', NULL, 'SCH-30004', 'oneTime', 'reuse_active', 'user-sch-admin'),
('inv-sch-05', true, -550000, 'cli-sch-05', NOW() - INTERVAL '8 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 550000, true, 'actual', 'Корпоративное обучение (Билим Плюс)', date_trunc('month', NOW()) + INTERVAL '1 month', 'SCH-30005', 'monthly', 'regenerate_per_period', 'user-sch-acc'),

-- Simple
('inv-sm-01', true, -2500000, 'cli-sm-01', NOW() - INTERVAL '15 days', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',
 2500000, false, 'actual', 'Аренда — ИП Нурланов', date_trunc('month', NOW()) + INTERVAL '1 month', 'SM-40001', 'monthly', 'reuse_active', 'user-sm-admin'),
('inv-sm-02', false, 0, 'cli-sm-02', NOW() - INTERVAL '7 days', NOW() - INTERVAL '7 days', NOW() + INTERVAL '23 days',
 500000, false, 'closed', 'Консультация — Сатыбалдиева Ж.', NULL, 'SM-40002', 'oneTime', 'reuse_active', 'user-sm-admin'),
('inv-sm-03', false, 0, 'cli-sm-03', NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days', NOW() + INTERVAL '27 days',
 80000, false, 'actual', 'Доставка — Юг-Логистик', NULL, 'SM-40003', 'oneTime', 'reuse_active', 'user-sm-admin');

INSERT INTO invoice_services (id, invoice, service, service_summ) VALUES
('is-med-01', 'inv-med-01', 'svc-med-consult', 150000),
('is-med-02', 'inv-med-02', 'svc-med-cardio',  250000),
('is-med-03a','inv-med-03', 'svc-med-echo',    350000),
('is-med-03b','inv-med-03', 'svc-med-blood',    45000),
('is-med-04', 'inv-med-04', 'svc-med-package', 890000),
('is-ds-01',  'inv-ds-01',  'svc-ds-fee',      850000),
('is-ds-02a', 'inv-ds-02',  'svc-ds-fee',      850000),
('is-ds-02b', 'inv-ds-02',  'svc-ds-food',     250000),
('is-ds-03a', 'inv-ds-03',  'svc-ds-fee',      850000),
('is-ds-03b', 'inv-ds-03',  'svc-ds-extra',    150000),
('is-ds-04a', 'inv-ds-04',  'svc-ds-fee',      850000),
('is-ds-04b', 'inv-ds-04',  'svc-ds-food',     250000),
('is-ds-05',  'inv-ds-05',  'svc-ds-camp',    1200000),
('is-sch-01', 'inv-sch-01', 'svc-sch-tuition', 550000),
('is-sch-02a','inv-sch-02', 'svc-sch-tuition', 550000),
('is-sch-02b','inv-sch-02', 'svc-sch-lunch',   180000),
('is-sch-03a','inv-sch-03', 'svc-sch-tuition', 550000),
('is-sch-03b','inv-sch-03', 'svc-sch-lunch',   180000),
('is-sch-04', 'inv-sch-04', 'svc-sch-uniform', 320000),
('is-sch-05', 'inv-sch-05', 'svc-sch-tuition', 550000),
('is-sm-01',  'inv-sm-01',  'svc-sm-rent',    2500000),
('is-sm-02',  'inv-sm-02',  'svc-sm-consult',  500000),
('is-sm-03',  'inv-sm-03',  'svc-sm-delivery',  80000);

INSERT INTO invoice_payments (id, invoice, date_from, date_to, payment_summ, payment_status, period_value) VALUES
-- Paid periods
('ip-med-01', 'inv-med-01', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', 150000, 'paid', NULL),
('ip-med-03', 'inv-med-03', NOW() - INTERVAL '4 days',  NOW() - INTERVAL '4 days',  395000, 'paid', NULL),
('ip-ds-02',  'inv-ds-02',  date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day', 1100000, 'paid', to_char(NOW(), 'YYYY-MM')),
('ip-ds-03',  'inv-ds-03',  date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',  750000, 'paid', to_char(NOW(), 'YYYY-MM')),
('ip-ds-05',  'inv-ds-05',  NOW() - INTERVAL '100 days', NOW() - INTERVAL '70 days', 1200000, 'paid', NULL),
('ip-sch-02', 'inv-sch-02', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day', 730000, 'paid', to_char(NOW(), 'YYYY-MM')),
('ip-sch-04', 'inv-sch-04', NOW() - INTERVAL '40 days', NOW() - INTERVAL '40 days', 320000, 'paid', NULL),
('ip-sm-02',  'inv-sm-02',  NOW() - INTERVAL '7 days',  NOW() - INTERVAL '7 days',  500000, 'paid', NULL),
('ip-sm-03',  'inv-sm-03',  NOW() - INTERVAL '3 days',  NOW() - INTERVAL '3 days',   80000, 'paid', NULL),
-- Unpaid
('ip-med-02', 'inv-med-02', NOW() - INTERVAL '5 days',  NOW() + INTERVAL '25 days', 250000, 'non_paid', NULL),
('ip-med-04', 'inv-med-04', NOW() - INTERVAL '1 day',   NOW() + INTERVAL '29 days', 890000, 'non_paid', NULL),
('ip-ds-01',  'inv-ds-01',  date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day', 850000, 'non_paid', to_char(NOW(), 'YYYY-MM')),
('ip-ds-04',  'inv-ds-04',  date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',1100000, 'non_paid', to_char(NOW(), 'YYYY-MM')),
('ip-sch-01', 'inv-sch-01', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day', 550000, 'non_paid', to_char(NOW(), 'YYYY-MM')),
('ip-sch-03', 'inv-sch-03', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day', 730000, 'non_paid', to_char(NOW(), 'YYYY-MM')),
('ip-sch-05', 'inv-sch-05', date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day', 550000, 'non_paid', to_char(NOW(), 'YYYY-MM')),
('ip-sm-01',  'inv-sm-01',  date_trunc('month', NOW()), date_trunc('month', NOW()) + INTERVAL '1 month' - INTERVAL '1 day',2500000, 'non_paid', to_char(NOW(), 'YYYY-MM'));

INSERT INTO invoice_qr (id, invoice_id, transaction, status, qr_link, qr_code_base64, created_at, disabled_at) VALUES
('qr-med-01', 'inv-med-01', 'txn-med-01', 'active', 'https://pay.demo.kg/qr/MED-10001', NULL, NOW() - INTERVAL '10 days', NULL),
('qr-med-02', 'inv-med-02', NULL,         'active', 'https://pay.demo.kg/qr/MED-10002', NULL, NOW() - INTERVAL '5 days', NULL),
('qr-ds-01',  'inv-ds-01',  NULL,         'active', 'https://pay.demo.kg/qr/DS-20001',  NULL, NOW() - INTERVAL '25 days', NULL),
('qr-ds-02',  'inv-ds-02',  'txn-ds-02',  'active', 'https://pay.demo.kg/qr/DS-20002',  NULL, NOW() - INTERVAL '20 days', NULL),
('qr-sch-01', 'inv-sch-01', NULL,         'active', 'https://pay.demo.kg/qr/SCH-30001', NULL, NOW() - INTERVAL '18 days', NULL),
('qr-sch-02', 'inv-sch-02', 'txn-sch-02', 'active', 'https://pay.demo.kg/qr/SCH-30002', NULL, NOW() - INTERVAL '18 days', NULL),
('qr-sm-01',  'inv-sm-01',  NULL,         'active', 'https://pay.demo.kg/qr/SM-40001',  NULL, NOW() - INTERVAL '15 days', NULL),
('qr-sm-02',  'inv-sm-02',  'txn-sm-02',  'active', 'https://pay.demo.kg/qr/SM-40002',  NULL, NOW() - INTERVAL '7 days', NULL);

-- ---------------------------------------------------------------------------
-- 18. Transactions
-- ---------------------------------------------------------------------------
INSERT INTO transactions (
    id, transaction_date, transaction_status, summ, transaction_summ,
    lower_commission_from_org, upper_commission_from_agent, lower_commission_to_agent,
    invoice, agent, payment_invoice, txn_id, transaction_system, transaction_type
) VALUES
('txn-med-01', NOW() - INTERVAL '9 days', 'success', 150000, 153000, 0, 3000, 0,
 'inv-med-01', 'agent-demo-01', 'ip-med-01', 'MB-100001', 'MBANK', 'debit'),
('txn-med-03', NOW() - INTERVAL '3 days', 'success', 395000, 402900, 0, 7900, 0,
 'inv-med-03', 'agent-demo-01', 'ip-med-03', 'MB-100002', 'MBANK', 'debit'),
('txn-ds-02',  NOW() - INTERVAL '8 days', 'success', 1100000, 1122000, 22000, 16500, 11000,
 'inv-ds-02', 'agent-demo-01', 'ip-ds-02', 'MB-200001', 'MBANK', 'debit'),
('txn-ds-03',  NOW() - INTERVAL '6 days', 'success', 750000, 765000, 15000, 11250, 7500,
 'inv-ds-03', 'agent-demo-01', 'ip-ds-03', 'MB-200002', 'MBANK', 'debit'),
('txn-ds-05',  NOW() - INTERVAL '90 days','success', 1200000, 1224000, 24000, 18000, 12000,
 'inv-ds-05', 'agent-demo-02', 'ip-ds-05', 'OP-200003', 'OPTIMA', 'debit'),
('txn-sch-02', NOW() - INTERVAL '5 days', 'success', 730000, 740950, 10950, 7300, 0,
 'inv-sch-02', 'agent-demo-01', 'ip-sch-02', 'MB-300001', 'MBANK', 'debit'),
('txn-sch-04', NOW() - INTERVAL '35 days','success', 320000, 324800, 4800, 3200, 0,
 'inv-sch-04', 'agent-demo-01', 'ip-sch-04', 'MB-300002', 'MBANK', 'debit'),
('txn-sm-02',  NOW() - INTERVAL '6 days', 'success', 500000, 500000, 0, 0, 0,
 'inv-sm-02', 'agent-demo-01', 'ip-sm-02', 'MB-400001', 'MBANK', 'debit'),
('txn-sm-03',  NOW() - INTERVAL '2 days', 'success', 80000, 80000, 0, 0, 0,
 'inv-sm-03', 'agent-demo-01', 'ip-sm-03', 'MB-400002', 'MBANK', 'debit'),
('txn-err-01', NOW() - INTERVAL '4 days', 'error', 850000, 850000, 0, 0, 0,
 'inv-ds-01', 'agent-demo-01', NULL, 'MB-200099', 'MBANK', 'debit');

-- ---------------------------------------------------------------------------
-- 19. Agent commissions (link agents ↔ orgs)
-- ---------------------------------------------------------------------------
INSERT INTO agent_commission (id, agent_id, organization_id, commission_id, lower_commission_id) VALUES
('ac-med-01', 'agent-demo-01', 'org-med-01',    'comm-percent-2',  NULL),
('ac-ds-01',  'agent-demo-01', '01000', 'comm-percent-15','comm-percent-2'),
('ac-ds-02',  'agent-demo-02', '01000', 'comm-percent-15','comm-fixed-5'),
('ac-sch-01', 'agent-demo-01', 'org-school-01', 'comm-percent-15', NULL),
('ac-sm-01',  'agent-demo-01', 'org-simple-01', 'comm-fixed-5',    NULL);

-- ---------------------------------------------------------------------------
-- 20. Notifications
-- ---------------------------------------------------------------------------
INSERT INTO notifications (
    id, client_id, channel, contact_info, subject, message, status,
    created_at, processed_at, sent_at, retry_count, error_message, metadata
) VALUES
('ntf-01', 'cli-ds-01', 'whatsapp', '+996555201001', 'Напоминание об оплате',
 'Уважаемые родители! Напоминаем об оплате за детский сад за текущий месяц. Л/с: DS-20001. Сумма: 8500 сом.',
 'sent', NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days', 0, NULL, '{"invoice":"inv-ds-01"}'),
('ntf-02', 'cli-ds-01', 'email', 'nurgul.alieva@mail.kg', 'Напоминание об оплате',
 'Добрый день! По счёту DS-20001 имеется задолженность 8500 сом.',
 'new', NOW() - INTERVAL '1 day', NULL, NULL, 0, NULL, '{"invoice":"inv-ds-01"}'),
('ntf-03', 'cli-sch-01', 'telegram', '@parent_ismailov', 'Оплата обучения',
 'Напоминание: оплата обучения за месяц, л/с SCH-30001, сумма 5500 сом.',
 'failed', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NULL, 2, 'Chat not found', '{"invoice":"inv-sch-01"}'),
('ntf-04', 'cli-med-02', 'whatsapp', '+996555101002', 'Оплата приёма',
 'Ожидается оплата консультации кардиолога. Л/с: MED-10002. Сумма: 2500 сом.',
 'processing', NOW() - INTERVAL '6 hours', NOW() - INTERVAL '5 hours', NULL, 0, NULL, '{"invoice":"inv-med-02"}'),
('ntf-05', 'cli-sm-01', 'email', 'nurlanov@mail.kg', 'Аренда за месяц',
 'Счёт на оплату аренды SM-40001 на сумму 25000 сом.',
 'sent', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', 0, NULL, NULL);

-- ---------------------------------------------------------------------------
-- 21. History (audit, keyless)
-- ---------------------------------------------------------------------------
INSERT INTO history (id, type_history, user_editor, invoice) VALUES
('hist-01', 'invoice_edited', 'user-ds-admin',  'inv-ds-01'),
('hist-02', 'invoice_edited', 'user-sch-admin', 'inv-sch-01'),
('hist-03', 'user_edited',    'user-med-admin', NULL),
('hist-04', 'invoice_edited', 'user-sm-admin',  'inv-sm-01');

COMMIT;

-- =============================================================================
-- КРАТКАЯ СПРАВКА ДЛЯ ДЕМО
-- =============================================================================
-- Пароль всех пользователей: Demo123!
--
-- Медклиника (medclinic):
--   admin@zdorovie.kg      — полный доступ
--   doctor@zdorovie.kg     — кабинет врача
--   doctor2@zdorovie.kg    — педиатр
--   registry@zdorovie.kg   — регистратура
--
-- Детский сад (detsad):
--   admin@balazhan.kg
--   teacher@balazhan.kg
--
-- Школа / standart:
--   admin@school42.kg
--   accountant@school42.kg
--
-- Simple:
--   admin@kasymov.kg
--
-- API агент: login=demo_agent  password=AgentApi2026!
-- =============================================================================
