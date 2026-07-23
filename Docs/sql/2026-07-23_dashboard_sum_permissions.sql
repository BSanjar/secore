-- Права на отображение сумм на главном экране по каналам оплаты.
-- Доступны для настройки у любой роли (area IS NULL).

BEGIN;

WITH seed(name, code, description, category, area) AS (
    VALUES
        ('Суммы: QR SECORE', 'dashboard.sums.qr_secore', 'Отображать суммы, принятые через QR SECORE', 'Дашборд', NULL),
        ('Суммы: QR внешний', 'dashboard.sums.qr_external', 'Отображать суммы, принятые через внешний QR', 'Дашборд', NULL),
        ('Суммы: наличные', 'dashboard.sums.cash', 'Отображать суммы, принятые через наличные', 'Дашборд', NULL),
        ('Суммы: карта', 'dashboard.sums.card', 'Отображать суммы, принятые через карту', 'Дашборд', NULL)
),
updated AS (
    UPDATE permissions p
       SET name = s.name,
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
        SELECT 1 FROM permissions p WHERE p.code = s.code
     )
),
id_base AS (
    SELECT COALESCE(MAX(id::int), 0) AS max_id
      FROM permissions
     WHERE id ~ '^[0-9]+$'
)
INSERT INTO permissions (id, name, code, description, category, isdeleted, area)
SELECT (b.max_id + ROW_NUMBER() OVER (ORDER BY m.code))::varchar,
       m.name,
       m.code,
       m.description,
       m.category,
       0,
       m.area
  FROM missing m
 CROSS JOIN id_base b;

COMMIT;
