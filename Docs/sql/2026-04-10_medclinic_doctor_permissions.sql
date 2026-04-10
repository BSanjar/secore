-- Права для роли "Врач" (medclinic).
-- Безопасный сценарий:
-- 1) восстанавливает soft-deleted записи (isdeleted = 1) по (code, area),
-- 2) добавляет только отсутствующие права (без дублей).

with desired_permissions as (
    select *
    from (
        values
            ('appointments.view', 'Просмотр календаря приемов', 'Право на просмотр страницы графика приемов', 'Приемы', 'medclinic'),
            ('appointments.edit', 'Редактирование приемов', 'Право на создание и редактирование приемов', 'Приемы', 'medclinic'),
            ('appointments.invoice', 'Счет из приема', 'Право на формирование счета на оплату из записи приема', 'Приемы', 'medclinic'),
            ('transactions.view', 'Просмотр транзакций', 'Право на просмотр транзакций', 'Транзакции', 'medclinic')
    ) as v(code, name, description, category, area)
),
restored as (
    update permissions p
    set
        name = d.name,
        description = d.description,
        category = d.category,
        isdeleted = 0
    from desired_permissions d
    where p.code = d.code
      and coalesce(p.area, '') = coalesce(d.area, '')
      and coalesce(p.isdeleted, 0) = 1
    returning p.id
),
missing as (
    select d.*
    from desired_permissions d
    where not exists (
        select 1
        from permissions p
        where p.code = d.code
          and coalesce(p.area, '') = coalesce(d.area, '')
          and coalesce(p.isdeleted, 0) = 0
    )
),
id_seed as (
    select coalesce(max(case when id ~ '^[0-9]+$' then id::integer end), 0) as max_id
    from permissions
),
missing_with_ids as (
    select
        (id_seed.max_id + row_number() over (order by m.code))::character varying as id,
        m.name,
        m.code,
        m.description,
        m.category,
        m.area
    from missing m
    cross join id_seed
)
insert into permissions (id, name, code, description, category, area, isdeleted)
select
    mw.id,
    mw.name,
    mw.code,
    mw.description,
    mw.category,
    mw.area,
    0
from missing_with_ids mw;