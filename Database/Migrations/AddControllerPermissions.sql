-- Скрипт для добавления прав доступа, используемых в контроллерах
-- Проверяет существование прав по коду и добавляет только отсутствующие
-- Автоматически определяет следующий доступный ID

-- Функция для получения следующего доступного ID
DO $$
DECLARE
    max_id INTEGER;
    new_id INTEGER;
BEGIN
    -- Находим максимальный числовой ID в таблице permissions
    SELECT COALESCE(MAX(CAST(id AS INTEGER)), 0) INTO max_id
    FROM permissions
    WHERE id ~ '^[0-9]+$';
    
    new_id := max_id + 1;
    
    RAISE NOTICE 'Начальный ID для новых прав: %', new_id;
    
    -- Добавление прав доступа
    -- Если право с таким кодом уже существует, оно не будет добавлено
    
    -- 1. Просмотр главной страницы (Dashboard) - общее право
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'dashboard.view' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Просмотр главной страницы', 'dashboard.view', 
                'Право на просмотр главной страницы (Dashboard)', 'Главная', NULL, 0);
        RAISE NOTICE 'Добавлено право: dashboard.view (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право dashboard.view уже существует, пропущено';
    END IF;
    
    -- 2. Просмотр транзакций - общее право (может уже существовать, но проверим)
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'transactions.view' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Просмотр транзакций', 'transactions.view', 
                'Право на просмотр транзакций', 'Транзакции', NULL, 0);
        RAISE NOTICE 'Добавлено право: transactions.view (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право transactions.view уже существует, пропущено';
    END IF;
    
    -- 3. Просмотр детей/клиентов - для детского сада
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'children.view' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Просмотр детей', 'children.view', 
                'Право на просмотр списка детей/клиентов', 'Дети', 'detsad', 0);
        RAISE NOTICE 'Добавлено право: children.view (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право children.view уже существует, пропущено';
    END IF;
    
    -- 4. Создание детей/клиентов - для детского сада
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'children.create' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Создание детей', 'children.create', 
                'Право на создание/добавление детей/клиентов', 'Дети', 'detsad', 0);
        RAISE NOTICE 'Добавлено право: children.create (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право children.create уже существует, пропущено';
    END IF;
    
    -- 5. Просмотр счетов - общее право (может уже существовать)

    
    -- 6. Создание платежей - общее право
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'payments.create' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Создание платежей', 'payments.create', 
                'Право на создание платежей', 'Платежи', NULL, 0);
        RAISE NOTICE 'Добавлено право: payments.create (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право payments.create уже существует, пропущено';
    END IF;
    
    -- Дополнительные права для полноты функционала
    
    -- 7. Редактирование детей - для детского сада
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'children.edit' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Редактирование детей', 'children.edit', 
                'Право на редактирование данных детей/клиентов', 'Дети', 'detsad', 0);
        RAISE NOTICE 'Добавлено право: children.edit (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право children.edit уже существует, пропущено';
    END IF;
    
    -- 8. Удаление детей - для детского сада
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'children.delete' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Удаление детей', 'children.delete', 
                'Право на удаление детей/клиентов', 'Дети', 'detsad', 0);
        RAISE NOTICE 'Добавлено право: children.delete (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право children.delete уже существует, пропущено';
    END IF;
    



    
    -- 12. Просмотр платежей - общее право
    IF NOT EXISTS (SELECT 1 FROM permissions WHERE code = 'payments.view' AND (isdeleted = 0 OR isdeleted IS NULL)) THEN
        INSERT INTO permissions (id, name, code, description, category, area, isdeleted)
        VALUES (new_id::VARCHAR, 'Просмотр платежей', 'payments.view', 
                'Право на просмотр платежей', 'Платежи', NULL, 0);
        RAISE NOTICE 'Добавлено право: payments.view (ID: %)', new_id;
        new_id := new_id + 1;
    ELSE
        RAISE NOTICE 'Право payments.view уже существует, пропущено';
    END IF;
    
    RAISE NOTICE 'Скрипт завершен. Следующий доступный ID: %', new_id;
END $$;

-- Вывод информации о добавленных правах
SELECT 
    id,
    name,
    code,
    category,
    area,
    CASE 
        WHEN area IS NULL THEN 'Общее право (доступно всем типам организаций)'
        ELSE 'Право для типа организации: ' || area
    END AS area_description
FROM permissions
WHERE code IN (
    'dashboard.view',
    'transactions.view',
    'children.view',
    'children.create',
    'children.edit',
    'children.delete',
    'invoices.view',
    'invoices.create',
    'invoices.edit',
    'payments.view',
    'payments.create',
    'transactions.create'
)
AND (isdeleted = 0 OR isdeleted IS NULL)
ORDER BY category, code;

