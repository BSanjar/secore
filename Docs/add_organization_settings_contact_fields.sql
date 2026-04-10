ALTER TABLE organization_settings
    ADD COLUMN IF NOT EXISTS email character varying,
    ADD COLUMN IF NOT EXISTS whatsapp_phone character varying,
    ADD COLUMN IF NOT EXISTS contact_phone character varying,
    ADD COLUMN IF NOT EXISTS director_full_name character varying,
    ADD COLUMN IF NOT EXISTS address character varying,
    ADD COLUMN IF NOT EXISTS logo_path character varying;
