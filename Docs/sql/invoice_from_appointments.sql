-- Счета, созданные из записи на приём (medclinic)
ALTER TABLE invoice
    ADD COLUMN IF NOT EXISTS from_appointments boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN invoice.from_appointments IS 'Счёт создан автоматически при записи на приём';
