-- Полный сброс пациентов medclinic (org-med-01) и пересоздание тестовых данных.
-- Удаляет всех пациентов организации и связанные записи/счета/уведомления.
-- Безопасно запускать повторно. При ошибке 25P02: ROLLBACK;

ROLLBACK;

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Удаление связанных данных medclinic
-- ---------------------------------------------------------------------------
DELETE FROM appointment_services
WHERE appointment_id IN (
    SELECT id FROM appointments WHERE organization_id = 'org-med-01'
);

DELETE FROM appointments
WHERE organization_id = 'org-med-01';

DELETE FROM notifications
WHERE client_id IN (
    SELECT id FROM organization_clients WHERE organization = 'org-med-01'
);

DELETE FROM invoice_qr
WHERE invoice_id IN (
    SELECT id FROM invoice
    WHERE client IN (SELECT id FROM organization_clients WHERE organization = 'org-med-01')
);

DELETE FROM transactions
WHERE invoice IN (
    SELECT id FROM invoice
    WHERE client IN (SELECT id FROM organization_clients WHERE organization = 'org-med-01')
);

DELETE FROM invoice_payments
WHERE invoice IN (
    SELECT id FROM invoice
    WHERE client IN (SELECT id FROM organization_clients WHERE organization = 'org-med-01')
);

DELETE FROM invoice_services
WHERE invoice IN (
    SELECT id FROM invoice
    WHERE client IN (SELECT id FROM organization_clients WHERE organization = 'org-med-01')
);

DELETE FROM invoice
WHERE client IN (
    SELECT id FROM organization_clients WHERE organization = 'org-med-01'
);

DELETE FROM organization_clients_additional_fields
WHERE organization_client IN (
    SELECT id FROM organization_clients WHERE organization = 'org-med-01'
);

DELETE FROM organization_clients
WHERE organization = 'org-med-01';

-- ---------------------------------------------------------------------------
-- 2. Доп. поля (если ещё не созданы)
-- ---------------------------------------------------------------------------
INSERT INTO organization_fields (id, field_name, field_type, field_select_values, isdeleted, organization, filterbyfield)
SELECT v.id, v.field_name, v.field_type, v.field_select_values, v.isdeleted, v.organization, v.filterbyfield
FROM (VALUES
    ('fld-med-blood',  'Группа крови',             'selected', 'I(0);II(A);III(B);IV(AB)', 0, 'org-med-01', true),
    ('fld-med-chronic','Хронические заболевания',  'string',   NULL,                       0, 'org-med-01', false),
    ('fld-med-gender', 'Пол',                      'selected', 'М;Ж',                      0, 'org-med-01', true),
    ('fld-med-birth',  'Дата рождения',            'datetime', NULL,                       0, 'org-med-01', true)
) AS v(id, field_name, field_type, field_select_values, isdeleted, organization, filterbyfield)
WHERE NOT EXISTS (SELECT 1 FROM organization_fields f WHERE f.id = v.id);

-- ---------------------------------------------------------------------------
-- 3. Новые тестовые пациенты (пол + дата рождения у всех активных)
-- ---------------------------------------------------------------------------
INSERT INTO organization_clients (
    id, organization, client_name, client_type, client_inn, client_phone, client_address,
    client_email, client_balance, client_status, created_date, updated_date, user_creater,
    client_wa, client_tg, org_client_group_id
) VALUES
('cli-med-01', 'org-med-01', 'Абдыкадыров Тимур Серикович',     'fiz', NULL, '+996555101001',
 'г. Бишкек, ул. Чуй 120, кв. 15', 'timur.abdy@mail.kg', 0, 1, NOW() - INTERVAL '60 days', NOW(), 'user-med-admin',
 '+996555101001', '@timur_ab', NULL),
('cli-med-02', 'org-med-01', 'Кожоева Айжан Маратовна',         'fiz', NULL, '+996555101002',
 'г. Бишкек, мкр. Асанбай 12-34', 'aizhan.k@gmail.com', -150000, 1, NOW() - INTERVAL '45 days', NOW(), 'user-med-admin',
 '+996555101002', NULL, NULL),
('cli-med-03', 'org-med-01', 'Сыдыков Бекзат Эрмекович',        'fiz', NULL, '+996555101003',
 'г. Бишкек, ул. Киевская 88', 'bekzat.s@mail.ru', 0, 1, NOW() - INTERVAL '30 days', NOW(), 'user-med-reg',
 '+996555101003', '@bekzat', NULL),
('cli-med-04', 'org-med-01', 'Осмонова Айпери Асылбековна',     'fiz', NULL, '+996555101004',
 'г. Бишкек, мкр. Джал 15-8', 'aiperi.o@gmail.com', 0, 1, NOW() - INTERVAL '20 days', NOW(), 'user-med-reg',
 '+996555101004', NULL, NULL),
('cli-med-05', 'org-med-01', 'Нурматова Самара Асылбековна',     'fiz', NULL, '+996555101005',
 'г. Бишкек, ул. Ахунбаева 22', 'samara.n@gmail.com', 0, 1, NOW() - INTERVAL '15 days', NOW(), 'user-med-admin',
 NULL, NULL, NULL),
('cli-med-06', 'org-med-01', 'Токтосунов Эрлан Бакытович',      'fiz', NULL, '+996555101006',
 'г. Бишкек, пр. Манаса 45', 'erlan.t@mail.kg', 500000, 1, NOW() - INTERVAL '40 days', NOW(), 'user-med-reg',
 '+996555101006', NULL, NULL),
('cli-med-07', 'org-med-01', 'Маматова Гульнара Талантбековна','fiz', NULL, '+996555101007',
 'г. Бишкек, мкр. Тунгуч 3-21', 'gulnara.m@gmail.com', 0, 1, NOW() - INTERVAL '25 days', NOW(), 'user-med-admin',
 '+996555101007', '@gulnara_m', NULL),
('cli-med-08', 'org-med-01', 'Ибраимов Данияр Канатович',       'fiz', NULL, '+996555101008',
 'г. Бишкек, ул. Исанова 67', NULL, 0, 0, NOW() - INTERVAL '5 days', NOW(), 'user-med-admin',
 NULL, NULL, NULL);

INSERT INTO organization_clients_additional_fields (id, field, organization_client, value) VALUES
-- cli-med-01: М, 48 лет (40+)
('af-med-01-gn', 'fld-med-gender', 'cli-med-01', 'М'),
('af-med-01-bd', 'fld-med-birth',  'cli-med-01', '1978-03-15'),
('af-med-01-bl', 'fld-med-blood',  'cli-med-01', 'II(A)'),
-- cli-med-02: Ж, 34 года (30+)
('af-med-02-gn', 'fld-med-gender', 'cli-med-02', 'Ж'),
('af-med-02-bd', 'fld-med-birth',  'cli-med-02', '1991-08-25'),
('af-med-02-bl', 'fld-med-blood',  'cli-med-02', 'I(0)'),
('af-med-02-ch', 'fld-med-chronic','cli-med-02', 'Гипертония'),
-- cli-med-03: М, 25 лет (18–30)
('af-med-03-gn', 'fld-med-gender', 'cli-med-03', 'М'),
('af-med-03-bd', 'fld-med-birth',  'cli-med-03', '2001-01-09'),
('af-med-03-bl', 'fld-med-blood',  'cli-med-03', 'III(B)'),
-- cli-med-04: Ж, 17 лет (младше 18)
('af-med-04-gn', 'fld-med-gender', 'cli-med-04', 'Ж'),
('af-med-04-bd', 'fld-med-birth',  'cli-med-04', '2009-06-12'),
-- cli-med-05: Ж, 28 лет (младше 30)
('af-med-05-gn', 'fld-med-gender', 'cli-med-05', 'Ж'),
('af-med-05-bd', 'fld-med-birth',  'cli-med-05', '1997-11-03'),
-- cli-med-06: М, 56 лет (40+)
('af-med-06-gn', 'fld-med-gender', 'cli-med-06', 'М'),
('af-med-06-bd', 'fld-med-birth',  'cli-med-06', '1970-05-20'),
('af-med-06-ch', 'fld-med-chronic','cli-med-06', 'Сахарный диабет 2 типа'),
-- cli-med-07: Ж, 38 лет (30+)
('af-med-07-gn', 'fld-med-gender', 'cli-med-07', 'Ж'),
('af-med-07-bd', 'fld-med-birth',  'cli-med-07', '1988-02-14'),
-- cli-med-08: М, 32 года (неактивный пациент)
('af-med-08-gn', 'fld-med-gender', 'cli-med-08', 'М'),
('af-med-08-bd', 'fld-med-birth',  'cli-med-08', '1994-07-08');

-- ---------------------------------------------------------------------------
-- 4. Записи на приём
-- ---------------------------------------------------------------------------
INSERT INTO appointments (
    id, organization_id, patient_id, doctor_id, title, phone, email, notes,
    referral_source, payment_type, appointment_status, is_active,
    starts_at, ends_at, created_at, updated_at, created_by
) VALUES
('apt-01', 'org-med-01', 'cli-med-01', 'user-med-doc1', 'Приём терапевта', '+996555101001', 'timur.abdy@mail.kg',
 'Первичный осмотр', 'самообращение', 'invoice_paid', 'active', true,
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '09:30',
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '10:00',
 NOW() - INTERVAL '2 days', NOW(), 'user-med-reg'),
('apt-02', 'org-med-01', 'cli-med-02', 'user-med-doc1', 'Консультация кардиолога', '+996555101002', 'aizhan.k@gmail.com',
 'Контроль АД', 'повторный', 'unpaid', 'active', true,
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '10:30',
 date_trunc('day', NOW()) + INTERVAL '1 day' + TIME '11:00',
 NOW() - INTERVAL '1 day', NOW(), 'user-med-reg'),
('apt-03', 'org-med-01', 'cli-med-04', 'user-med-doc2', 'Приём педиатра', '+996555101004', 'aiperi.o@gmail.com',
 'Плановый осмотр', 'рекомендация', 'none', 'active', true,
 date_trunc('day', NOW()) + INTERVAL '2 days' + TIME '11:00',
 date_trunc('day', NOW()) + INTERVAL '2 days' + TIME '11:20',
 NOW(), NOW(), 'user-med-reg'),
('apt-04', 'org-med-01', 'cli-med-01', 'user-med-doc1', 'ЭхоКГ', '+996555101001', 'timur.abdy@mail.kg',
 'По направлению терапевта', 'направление', 'invoice_paid', 'active', true,
 date_trunc('day', NOW()) - INTERVAL '3 days' + TIME '14:00',
 date_trunc('day', NOW()) - INTERVAL '3 days' + TIME '14:30',
 NOW() - INTERVAL '5 days', NOW() - INTERVAL '3 days', 'user-med-admin'),
('apt-05', 'org-med-01', 'cli-med-06', 'user-med-doc1', 'Чек-ап «Базовый»', '+996555101006', 'erlan.t@mail.kg',
 'Профилактический осмотр', 'самообращение', 'unpaid', 'draft', true,
 date_trunc('day', NOW()) + INTERVAL '5 days' + TIME '09:00',
 date_trunc('day', NOW()) + INTERVAL '5 days' + TIME '10:00',
 NOW(), NOW(), 'user-med-admin'),
('apt-06', 'org-med-01', 'cli-med-08', 'user-med-doc2', 'Отменённый приём', '+996555101008', NULL,
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
-- 5. Счета и оплаты
-- ---------------------------------------------------------------------------
INSERT INTO invoice (
    id, auto_prolongation, balance, client, date_created, date_start_invoice, date_end_invoice,
    fixed_summ, hassameaccount, invoice_status, name_invoice, next_start_invoice,
    pay_code, periodicity, qr_mode, user_creater
) VALUES
('inv-med-01', false, 0, 'cli-med-01', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', NOW() + INTERVAL '20 days',
 150000, false, 'actual', 'Приём терапевта — Абдыкадыров Т.', NULL, 'MED-10001', 'oneTime', 'reuse_active', 'user-med-admin'),
('inv-med-02', false, -250000, 'cli-med-02', NOW() - INTERVAL '5 days', NOW() - INTERVAL '5 days', NOW() + INTERVAL '25 days',
 250000, false, 'actual', 'Кардиолог — Кожоева А.', NULL, 'MED-10002', 'oneTime', 'reuse_active', 'user-med-reg'),
('inv-med-03', false, 0, 'cli-med-01', NOW() - INTERVAL '4 days', NOW() - INTERVAL '4 days', NOW() + INTERVAL '26 days',
 395000, false, 'closed', 'ЭхоКГ + ОАК — Абдыкадыров Т.', NULL, 'MED-10003', 'oneTime', 'reuse_active', 'user-med-admin'),
('inv-med-04', false, -890000, 'cli-med-06', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', NOW() + INTERVAL '29 days',
 890000, false, 'actual', 'Чек-ап — Токтосунов Э.', NULL, 'MED-10004', 'oneTime', 'reuse_active', 'user-med-admin');

INSERT INTO invoice_services (id, invoice, service, service_summ) VALUES
('is-med-01', 'inv-med-01', 'svc-med-consult', 150000),
('is-med-02', 'inv-med-02', 'svc-med-cardio',  250000),
('is-med-03a','inv-med-03', 'svc-med-echo',    350000),
('is-med-03b','inv-med-03', 'svc-med-blood',    45000),
('is-med-04', 'inv-med-04', 'svc-med-package', 890000);

INSERT INTO invoice_payments (id, invoice, date_from, date_to, payment_summ, payment_status, period_value) VALUES
('ip-med-01', 'inv-med-01', NOW() - INTERVAL '10 days', NOW() - INTERVAL '10 days', 150000, 'paid', NULL),
('ip-med-03', 'inv-med-03', NOW() - INTERVAL '4 days',  NOW() - INTERVAL '4 days',  395000, 'paid', NULL),
('ip-med-02', 'inv-med-02', NOW() - INTERVAL '5 days',  NOW() + INTERVAL '25 days', 250000, 'non_paid', NULL),
('ip-med-04', 'inv-med-04', NOW() - INTERVAL '1 day',   NOW() + INTERVAL '29 days', 890000, 'non_paid', NULL);

INSERT INTO invoice_qr (id, invoice_id, transaction, status, qr_link, qr_code_base64, created_at, disabled_at) VALUES
('qr-med-01', 'inv-med-01', 'txn-med-01', 'active', 'https://pay.demo.kg/qr/MED-10001', NULL, NOW() - INTERVAL '10 days', NULL),
('qr-med-02', 'inv-med-02', NULL,         'active', 'https://pay.demo.kg/qr/MED-10002', NULL, NOW() - INTERVAL '5 days', NULL);

INSERT INTO transactions (
    id, transaction_date, transaction_status, summ, transaction_summ,
    lower_commission_from_org, upper_commission_from_agent, lower_commission_to_agent,
    invoice, agent, payment_invoice, txn_id, transaction_system, transaction_type
) VALUES
('txn-med-01', NOW() - INTERVAL '9 days', 'success', 150000, 153000, 0, 3000, 0,
 'inv-med-01', 'agent-demo-01', 'ip-med-01', 'MB-100001', 'MBANK', 'debit'),
('txn-med-03', NOW() - INTERVAL '3 days', 'success', 395000, 402900, 0, 7900, 0,
 'inv-med-03', 'agent-demo-01', 'ip-med-03', 'MB-100002', 'MBANK', 'debit')
ON CONFLICT (id) DO NOTHING;

INSERT INTO notifications (
    id, client_id, channel, contact_info, subject, message, status,
    created_at, processed_at, sent_at, retry_count, error_message, metadata
) VALUES
('ntf-04', 'cli-med-02', 'whatsapp', '+996555101002', 'Оплата приёма',
 'Напоминание об оплате консультации кардиолога.', 'sent',
 NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', 0, NULL, NULL)
ON CONFLICT (id) DO NOTHING;

COMMIT;
