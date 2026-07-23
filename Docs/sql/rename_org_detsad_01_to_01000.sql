-- Rename organization id: org-detsad-01 → 01000
-- Updates all known referencing tables in one transaction.
-- Safe to re-run only if old id still exists and new id does not.

BEGIN;

DO $$
DECLARE
    old_id text := 'org-detsad-01';
    new_id text := '01000';
    cnt integer;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM organization WHERE id = old_id) THEN
        RAISE EXCEPTION 'Source organization "%" not found', old_id;
    END IF;

    IF EXISTS (SELECT 1 FROM organization WHERE id = new_id) THEN
        RAISE EXCEPTION 'Target organization "%" already exists', new_id;
    END IF;

    -- 1) Clone parent row under new id
    INSERT INTO organization (id, name, organizationtype, is_active)
    SELECT new_id, name, organizationtype, is_active
    FROM organization
    WHERE id = old_id;

    -- 2) Child tables (FK / soft refs)
    UPDATE agent_commission                  SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE appointment_settings              SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE appointments                      SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE appointment_medical_templates     SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE departments                       SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE org_client_groups                 SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE organization_clients              SET organization    = new_id WHERE organization    = old_id;
    UPDATE organization_fields               SET organization    = new_id WHERE organization    = old_id;
    UPDATE organization_services             SET organization    = new_id WHERE organization    = old_id;
    UPDATE organization_settings             SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE organization_subscription         SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE organization_subscription_payment SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE roles                             SET organization    = new_id WHERE organization    = old_id;
    UPDATE specializations                   SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE user_work_schedules               SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE user_work_schedule_overrides      SET organization_id = new_id WHERE organization_id = old_id;
    UPDATE users                             SET organization    = new_id WHERE organization    = old_id;

    -- 3) Remove old parent
    DELETE FROM organization WHERE id = old_id;

    -- 4) Sanity: no leftovers
    SELECT COUNT(*) INTO cnt FROM (
        SELECT 1 FROM agent_commission WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM appointment_settings WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM appointments WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM appointment_medical_templates WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM departments WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM org_client_groups WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM organization_clients WHERE organization = old_id
        UNION ALL SELECT 1 FROM organization_fields WHERE organization = old_id
        UNION ALL SELECT 1 FROM organization_services WHERE organization = old_id
        UNION ALL SELECT 1 FROM organization_settings WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM organization_subscription WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM organization_subscription_payment WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM roles WHERE organization = old_id
        UNION ALL SELECT 1 FROM specializations WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM user_work_schedules WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM user_work_schedule_overrides WHERE organization_id = old_id
        UNION ALL SELECT 1 FROM users WHERE organization = old_id
    ) t;

    IF cnt > 0 THEN
        RAISE EXCEPTION 'Rename incomplete: % leftover references to %', cnt, old_id;
    END IF;

    RAISE NOTICE 'OK: organization id renamed % → %', old_id, new_id;
END $$;

COMMIT;

-- Verify
SELECT id, name, organizationtype, is_active
FROM organization
WHERE id IN ('org-detsad-01', '01000');

SELECT 'users' AS tbl, COUNT(*) AS cnt FROM users WHERE organization = '01000'
UNION ALL SELECT 'organization_clients', COUNT(*) FROM organization_clients WHERE organization = '01000'
UNION ALL SELECT 'org_client_groups', COUNT(*) FROM org_client_groups WHERE organization_id = '01000'
UNION ALL SELECT 'organization_services', COUNT(*) FROM organization_services WHERE organization = '01000'
UNION ALL SELECT 'roles', COUNT(*) FROM roles WHERE organization = '01000'
UNION ALL SELECT 'organization_settings', COUNT(*) FROM organization_settings WHERE organization_id = '01000';
