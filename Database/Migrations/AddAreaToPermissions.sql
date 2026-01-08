-- Миграция: добавление поля area в таблицу permissions
-- Этот скрипт нужно выполнить, если таблица permissions уже существует

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

-- Создание индекса для area, если его еще нет
CREATE INDEX IF NOT EXISTS idx_permissions_area ON permissions(area);

