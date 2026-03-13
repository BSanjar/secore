-- Подписочная модель: is_active у организации, таблицы organization_subscription и organization_subscription_payment

-- 1. Колонка is_active в organization
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'organization' AND column_name = 'is_active'
    ) THEN
        ALTER TABLE organization ADD COLUMN is_active BOOLEAN NOT NULL DEFAULT true;
        COMMENT ON COLUMN organization.is_active IS 'false — доступ заблокирован (истёк период подписки)';
    END IF;
END $$;

-- 2. Таблица organization_subscription (настройки тарифа подписки)
CREATE TABLE IF NOT EXISTS organization_subscription (
    id CHARACTER VARYING NOT NULL PRIMARY KEY,
    organization_id CHARACTER VARYING NOT NULL UNIQUE REFERENCES organization(id) ON DELETE CASCADE,
    price_tyiyn NUMERIC(18,2) NOT NULL DEFAULT 0,
    period_type CHARACTER VARYING NOT NULL DEFAULT 'month',
    created_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE
);
COMMENT ON TABLE organization_subscription IS 'Настройки подписочного тарифа: стоимость и период (месяц/год)';

-- 3. Таблица organization_subscription_payment (история оплат)
CREATE TABLE IF NOT EXISTS organization_subscription_payment (
    id CHARACTER VARYING NOT NULL PRIMARY KEY,
    organization_id CHARACTER VARYING NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
    paid_at TIMESTAMP WITH TIME ZONE NOT NULL,
    amount_tyiyn NUMERIC(18,2) NOT NULL,
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    note CHARACTER VARYING,
    created_at TIMESTAMP WITH TIME ZONE
);
COMMENT ON TABLE organization_subscription_payment IS 'Оплаты подписки: каждый платёж задаёт период доступа (period_start — period_end)';

CREATE INDEX IF NOT EXISTS ix_organization_subscription_payment_organization_id
    ON organization_subscription_payment(organization_id);
CREATE INDEX IF NOT EXISTS ix_organization_subscription_payment_period
    ON organization_subscription_payment(organization_id, period_start, period_end);
