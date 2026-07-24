BEGIN;

-- Право на создание и редактирование карточек сотрудников (не для регистратуры).
WITH seed("name", code, description, category, area) AS (
    VALUES
        ('Редактирование сотрудников', 'doctors.edit', 'Создание и изменение карточек сотрудников', 'Медструктура', 'medclinic')
),
updated AS (
    UPDATE public.permissions p
       SET "name" = s."name",
           description = s.description,
           category = s.category,
           area = s.area,
           isdeleted = 0
      FROM seed s
     WHERE p.code = s.code
    RETURNING p.code
),
missing AS (
    SELECT s.*
      FROM seed s
     WHERE NOT EXISTS (
        SELECT 1
          FROM public.permissions p
         WHERE p.code = s.code
     )
),
id_base AS (
    SELECT COALESCE(MAX(id::int), 0) AS max_id
      FROM public.permissions
     WHERE id ~ '^[0-9]+$'
)
INSERT INTO public.permissions (id, "name", code, description, category, isdeleted, area)
SELECT (b.max_id + ROW_NUMBER() OVER (ORDER BY m.code))::varchar AS id,
       m."name",
       m.code,
       m.description,
       m.category,
       0 AS isdeleted,
       m.area
  FROM missing m
 CROSS JOIN id_base b;

-- Администраторам medclinic выдать право (роли с полным набором medclinic-прав).
INSERT INTO role_permissions (id, role, permission, isdeleted)
SELECT 'rp-doc-edit-' || r.id || '-' || p.id,
       r.id,
       p.id,
       0
  FROM roles r
  JOIN permissions p ON p.code = 'doctors.edit' AND (p.isdeleted IS NULL OR p.isdeleted = 0)
 WHERE (r.isdeleted IS NULL OR r.isdeleted = 0)
   AND lower(r.name) IN ('администратор', 'admin')
   AND EXISTS (
       SELECT 1
         FROM organization o
        WHERE o.id = r.organization
          AND lower(o.organizationtype) = 'medclinic'
   )
   AND NOT EXISTS (
       SELECT 1
         FROM role_permissions rp
        WHERE rp.role = r.id
          AND rp.permission = p.id
          AND (rp.isdeleted IS NULL OR rp.isdeleted = 0)
   );

COMMIT;
