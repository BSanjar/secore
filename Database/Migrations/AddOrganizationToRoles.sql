-- Миграция: добавление поля organization в таблицу roles
-- Этот скрипт нужно выполнить, если таблица roles уже существует

-- Добавление поля organization, если его еще нет
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'roles' 
        AND column_name = 'organization'
    ) THEN
        ALTER TABLE roles ADD COLUMN organization CHARACTER VARYING;
    END IF;
END $$;

-- Добавление внешнего ключа для organization, если его еще нет
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'roles_fk_organization') THEN
        ALTER TABLE roles 
        ADD CONSTRAINT roles_fk_organization 
        FOREIGN KEY (organization) REFERENCES organization(id);
    END IF;
END $$;

-- Создание индекса для organization, если его еще нет
CREATE INDEX IF NOT EXISTS idx_roles_organization ON roles(organization);

