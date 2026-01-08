-- Миграция: удаление поля organization из таблицы role_permissions
-- Этот скрипт нужно выполнить, если таблица role_permissions уже существует с полем organization

-- Удаление внешнего ключа для organization, если он существует
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_fk_2') THEN
        ALTER TABLE role_permissions DROP CONSTRAINT role_permissions_fk_2;
    END IF;
END $$;

-- Удаление старого уникального ограничения, если оно существует
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_unique') THEN
        ALTER TABLE role_permissions DROP CONSTRAINT role_permissions_unique;
    END IF;
END $$;

-- Удаление индекса для organization, если он существует
DROP INDEX IF EXISTS idx_role_permissions_organization;

-- Удаление колонки organization, если она существует
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'role_permissions' 
        AND column_name = 'organization'
    ) THEN
        ALTER TABLE role_permissions DROP COLUMN organization;
    END IF;
END $$;

-- Добавление нового уникального ограничения (роль + право)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'role_permissions_unique') THEN
        ALTER TABLE role_permissions 
        ADD CONSTRAINT role_permissions_unique 
        UNIQUE (role, permission);
    END IF;
END $$;

