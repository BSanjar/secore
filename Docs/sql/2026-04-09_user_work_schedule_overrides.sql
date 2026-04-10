create table if not exists user_work_schedule_overrides
(
    id character varying primary key,
    organization_id character varying not null,
    user_id character varying not null,
    work_date date not null,
    is_working boolean not null default true,
    start_time time without time zone null,
    end_time time without time zone null,
    comment character varying null,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    constraint user_work_schedule_overrides_fk_organization
        foreign key (organization_id) references organization (id) on delete cascade,
    constraint user_work_schedule_overrides_fk_user
        foreign key (user_id) references users (id) on delete cascade
);

create unique index if not exists idx_user_work_schedule_overrides_user_date
    on user_work_schedule_overrides (user_id, work_date);

create index if not exists idx_user_work_schedule_overrides_organization_id
    on user_work_schedule_overrides (organization_id);
