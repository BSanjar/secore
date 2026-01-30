-- Миграция: добавление полей is_deleted, logo, created_date в таблицу org_client_groups

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'org_client_groups' AND column_name = 'is_deleted'
    ) THEN
        ALTER TABLE org_client_groups ADD COLUMN is_deleted INTEGER NOT NULL DEFAULT 0;
        COMMENT ON COLUMN org_client_groups.is_deleted IS '0 — активна, 1 — удалена (мягкое удаление)';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'org_client_groups' AND column_name = 'logo'
    ) THEN
        ALTER TABLE org_client_groups ADD COLUMN logo CHARACTER VARYING;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'org_client_groups' AND column_name = 'created_date'
    ) THEN
        ALTER TABLE org_client_groups ADD COLUMN created_date TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_org_client_groups_is_deleted ON org_client_groups(is_deleted);
