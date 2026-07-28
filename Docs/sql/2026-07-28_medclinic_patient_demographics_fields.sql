-- Дополнительные поля «Пол» и «Дата рождения» для medclinic (рассылка по фильтрам).
-- Безопасно запускать повторно на уже заполненной demo-БД.
-- При ошибке 25P02 сначала выполните: ROLLBACK;

ROLLBACK;

BEGIN;

INSERT INTO organization_fields (id, field_name, field_type, field_select_values, isdeleted, organization, filterbyfield)
SELECT v.id, v.field_name, v.field_type, v.field_select_values, v.isdeleted, v.organization, v.filterbyfield
FROM (VALUES
    ('fld-med-gender', 'Пол',            'selected', 'М;Ж', 0, 'org-med-01', true),
    ('fld-med-birth',  'Дата рождения',  'datetime', NULL,  0, 'org-med-01', true)
) AS v(id, field_name, field_type, field_select_values, isdeleted, organization, filterbyfield)
WHERE NOT EXISTS (SELECT 1 FROM organization_fields f WHERE f.id = v.id);

INSERT INTO organization_clients_additional_fields (id, field, organization_client, value)
SELECT v.id, v.field, v.organization_client, v.value
FROM (VALUES
    ('af-med-01-gn', 'fld-med-gender', 'cli-med-01', 'М'),
    ('af-med-01-bd', 'fld-med-birth',  'cli-med-01', '1985-04-12'),
    ('af-med-02-gn', 'fld-med-gender', 'cli-med-02', 'Ж'),
    ('af-med-02-bd', 'fld-med-birth',  'cli-med-02', '1992-08-25'),
    ('af-med-04-gn', 'fld-med-gender', 'cli-med-04', 'М'),
    ('af-med-04-bd', 'fld-med-birth',  'cli-med-04', '2001-01-09')
) AS v(id, field, organization_client, value)
WHERE NOT EXISTS (SELECT 1 FROM organization_clients_additional_fields af WHERE af.id = v.id);

COMMIT;
