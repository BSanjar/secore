-- Связь возврата (credit) с исходной транзакцией оплаты.

ALTER TABLE transactions
    ADD COLUMN IF NOT EXISTS parent_transaction character varying;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'transactions_fk_parent'
    ) THEN
        ALTER TABLE transactions
            ADD CONSTRAINT transactions_fk_parent
            FOREIGN KEY (parent_transaction) REFERENCES transactions (id);
    END IF;
END $$;

COMMENT ON COLUMN transactions.parent_transaction IS 'Исходная транзакция, по которой выполнен возврат (credit).';
