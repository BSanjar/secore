do
$$
begin
    if exists (
        select 1
        from information_schema.tables
        where table_schema = 'public'
          and table_name = 'doctor_work_schedules'
    ) and not exists (
        select 1
        from information_schema.tables
        where table_schema = 'public'
          and table_name = 'user_work_schedules'
    ) then
        alter table public.doctor_work_schedules rename to user_work_schedules;
    end if;
end
$$;

do
$$
begin
    if exists (
        select 1
        from information_schema.columns
        where table_schema = 'public'
          and table_name = 'user_work_schedules'
          and column_name = 'doctor_id'
    ) and not exists (
        select 1
        from information_schema.columns
        where table_schema = 'public'
          and table_name = 'user_work_schedules'
          and column_name = 'user_id'
    ) then
        alter table public.user_work_schedules rename column doctor_id to user_id;
    end if;
end
$$;

alter table if exists public.user_work_schedules
    drop constraint if exists doctor_work_schedules_fk_doctor;

alter table if exists public.user_work_schedules
    drop constraint if exists doctor_work_schedules_fk_user;

alter table if exists public.user_work_schedules
    drop constraint if exists doctor_work_schedules_fk_organization;

alter table if exists public.user_work_schedules
    add constraint user_work_schedules_fk_organization
        foreign key (organization_id) references organization (id) on delete cascade;

alter table if exists public.user_work_schedules
    add constraint user_work_schedules_fk_user
        foreign key (user_id) references users (id) on delete cascade;

alter index if exists idx_doctor_work_schedules_doctor_day
    rename to idx_user_work_schedules_user_day;

alter index if exists idx_doctor_work_schedules_organization_id
    rename to idx_user_work_schedules_organization_id;

create table if not exists user_work_schedules
(
    id character varying primary key,
    organization_id character varying not null,
    user_id character varying not null,
    day_of_week integer not null,
    start_time time without time zone not null,
    end_time time without time zone not null,
    is_active boolean not null default true,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    constraint user_work_schedules_fk_organization
        foreign key (organization_id) references organization (id) on delete cascade,
    constraint user_work_schedules_fk_user
        foreign key (user_id) references users (id) on delete cascade
);

create unique index if not exists idx_user_work_schedules_user_day
    on user_work_schedules (user_id, day_of_week);

create index if not exists idx_user_work_schedules_organization_id
    on user_work_schedules (organization_id);
