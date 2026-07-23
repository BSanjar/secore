-- QR: одна активная запись на лицевой счёт (pay_code), не на каждый invoice.
-- Выполнить на БД после деплоя кода.

ALTER TABLE invoice_qr
    ADD COLUMN IF NOT EXISTS pay_code character varying;

UPDATE invoice_qr q
SET pay_code = i.pay_code
FROM invoice i
WHERE q.invoice_id = i.id
  AND (q.pay_code IS NULL OR q.pay_code = '');

-- Оставляем одну запись на pay_code (последняя по created_at), остальные удаляем-дубли.
DELETE FROM invoice_qr a
    USING invoice_qr b
WHERE a.pay_code IS NOT NULL
  AND b.pay_code IS NOT NULL
  AND a.pay_code = b.pay_code
  AND a.id <> b.id
  AND (a.created_at < b.created_at OR (a.created_at = b.created_at AND a.id < b.id));

DROP INDEX IF EXISTS ux_invoice_qr_active_per_invoice;

CREATE UNIQUE INDEX IF NOT EXISTS ux_invoice_qr_pay_code
    ON invoice_qr (pay_code)
    WHERE pay_code IS NOT NULL AND pay_code <> '';

CREATE INDEX IF NOT EXISTS idx_invoice_qr_pay_code
    ON invoice_qr (pay_code);
