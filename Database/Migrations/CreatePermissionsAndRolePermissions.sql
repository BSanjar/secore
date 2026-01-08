-- Создание таблицы permissions (права доступа)
CREATE TABLE IF NOT EXISTS permissions (
    id CHARACTER VARYING PRIMARY KEY,
    name CHARACTER VARYING,
    code CHARACTER VARYING UNIQUE,
    description CHARACTER VARYING,
    category CHARACTER VARYING,
    area CHARACTER VARYING,
    isdeleted INTEGER DEFAULT 0
);

-- Добавление поля area, если его еще нет
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'permissions' 
        AND column_name = 'area'
    ) THEN
        ALTER TABLE permissions ADD COLUMN area CHARACTER VARYING;
    END IF;
END $$;

-- Создание таблицы role_permissions (связь ролей и прав)
CREATE TABLE IF NOT EXISTS role_permissions (
    id CHARACTER VARYING PRIMARY KEY,
    role CHARACTER VARYING NOT NULL,
    permission CHARACTER VARYING NOT NULL,
    isdeleted INTEGER DEFAULT 0
);

-- Добавление внешних ключей и ограничений
DO $$
BEGIN
    -- Добавление внешнего ключа для role
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_fk') THEN
        ALTER TABLE role_permissions ADD CONSTRAINT role_permissions_fk FOREIGN KEY (role) REFERENCES roles(id);
    END IF;
    
    -- Добавление внешнего ключа для permission (после создания таблицы permissions)
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_fk_1') THEN
        ALTER TABLE role_permissions ADD CONSTRAINT role_permissions_fk_1 FOREIGN KEY (permission) REFERENCES permissions(id);
    END IF;
    
    -- Добавление уникального ограничения (роль + право)
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_unique') THEN
        ALTER TABLE role_permissions ADD CONSTRAINT role_permissions_unique UNIQUE (role, permission);
    END IF;
END $$;

-- Создание индексов для улучшения производительности
CREATE INDEX IF NOT EXISTS idx_permissions_code ON permissions(code);
CREATE INDEX IF NOT EXISTS idx_permissions_category ON permissions(category);
CREATE INDEX IF NOT EXISTS idx_permissions_area ON permissions(area);
CREATE INDEX IF NOT EXISTS idx_permissions_isdeleted ON permissions(isdeleted);
CREATE INDEX IF NOT EXISTS idx_role_permissions_role ON role_permissions(role);
CREATE INDEX IF NOT EXISTS idx_role_permissions_permission ON role_permissions(permission);
CREATE INDEX IF NOT EXISTS idx_role_permissions_isdeleted ON role_permissions(isdeleted);

-- Добавление первичных ключей (если они еще не существуют)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'permissions_pk') THEN
        ALTER TABLE permissions ADD CONSTRAINT permissions_pk PRIMARY KEY (id);
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_pk') THEN
        ALTER TABLE role_permissions ADD CONSTRAINT role_permissions_pk PRIMARY KEY (id);
    END IF;
END $$;

-- Вставка базовых прав доступа (примеры)
-- Общие права (area = NULL) доступны всем типам организаций
INSERT INTO permissions (id, name, code, description, category, area, isdeleted) VALUES
('1', 'Просмотр пользователей', 'users.view', 'Право на просмотр списка пользователей', 'Пользователи', NULL, 0),
('2', 'Создание пользователей', 'users.create', 'Право на создание новых пользователей', 'Пользователи', NULL, 0),
('3', 'Редактирование пользователей', 'users.edit', 'Право на редактирование пользователей', 'Пользователи', NULL, 0),
('4', 'Удаление пользователей', 'users.delete', 'Право на удаление пользователей', 'Пользователи', NULL, 0),
('5', 'Просмотр счетов', 'invoices.view', 'Право на просмотр счетов', 'Счета', NULL, 0),
('6', 'Создание счетов', 'invoices.create', 'Право на создание новых счетов', 'Счета', NULL, 0),
('7', 'Редактирование счетов', 'invoices.edit', 'Право на редактирование счетов', 'Счета', NULL, 0),
('8', 'Удаление счетов', 'invoices.delete', 'Право на удаление счетов', 'Счета', NULL, 0),
('9', 'Просмотр транзакций', 'transactions.view', 'Право на просмотр транзакций', 'Транзакции', NULL, 0),
('10', 'Создание транзакций', 'transactions.create', 'Право на создание транзакций', 'Транзакции', NULL, 0),
('11', 'Редактирование транзакций', 'transactions.edit', 'Право на редактирование транзакций', 'Транзакции', NULL, 0),
('12', 'Удаление транзакций', 'transactions.delete', 'Право на удаление транзакций', 'Транзакции', NULL, 0),
('13', 'Просмотр клиентов', 'clients.view', 'Право на просмотр клиентов', 'Клиенты', NULL, 0),
('14', 'Создание клиентов', 'clients.create', 'Право на создание клиентов', 'Клиенты', NULL, 0),
('15', 'Редактирование клиентов', 'clients.edit', 'Право на редактирование клиентов', 'Клиенты', NULL, 0),
('16', 'Удаление клиентов', 'clients.delete', 'Право на удаление клиентов', 'Клиенты', NULL, 0),
('17', 'Управление ролями', 'roles.manage', 'Право на управление ролями и правами доступа', 'Администрирование', NULL, 0),
('18', 'Управление правами', 'permissions.manage', 'Право на управление правами доступа', 'Администрирование', NULL, 0),
('19', 'Назначение ролей', 'userroles.assign', 'Право на назначение ролей пользователям', 'Администрирование', NULL, 0)
ON CONFLICT (id) DO NOTHING;

-- Примеры прав для конкретных типов организаций
-- Права для детского сада (detsad)
INSERT INTO permissions (id, name, code, description, category, area, isdeleted) VALUES
('20', 'Просмотр детей', 'children.view', 'Право на просмотр списка детей', 'Дети', 'detsad', 0),
('21', 'Создание детей', 'children.create', 'Право на добавление детей', 'Дети', 'detsad', 0),
('22', 'Редактирование детей', 'children.edit', 'Право на редактирование данных детей', 'Дети', 'detsad', 0),
('23', 'Удаление детей', 'children.delete', 'Право на удаление детей', 'Дети', 'detsad', 0)
ON CONFLICT (id) DO NOTHING;

