BEGIN;

-- Seed/repair permissions for medclinic doctor flow.
-- Rules:
-- 1) No duplicates by `code`.
-- 2) If permission exists, it is updated and re-activated (isdeleted = 0).
-- 3) Missing permissions are inserted with incremental varchar `id`.

WITH seed("name", code, description, category, area) AS (
    VALUES
        ('Просмотр расписания и приемов', 'appointments.view', 'Право на просмотр календаря приемов', 'Записи', 'medclinic'),
        ('Редактирование приемов', 'appointments.edit', 'Право на создание и редактирование приемов', 'Записи', 'medclinic'),
        ('Счет из приема', 'appointments.invoice', 'Право на формирование счета на оплату из приема', 'Записи', 'medclinic'),
        ('Просмотр транзакций', 'transactions.view', 'Право на просмотр транзакций', 'Транзакции', NULL)
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

COMMIT;
