create table if not exists departments
(
    id character varying primary key,
    organization_id character varying not null,
    name character varying not null,
    code character varying null,
    description text null,
    is_active boolean not null default true,
    sort_order integer not null default 0,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    constraint departments_fk_organization
        foreign key (organization_id) references organization (id) on delete cascade
);

create unique index if not exists ux_departments_org_name on departments (organization_id, name);
create index if not exists idx_departments_organization_id on departments (organization_id);

create table if not exists specializations
(
    id character varying primary key,
    organization_id character varying not null,
    department_id character varying null,
    name character varying not null,
    description text null,
    is_active boolean not null default true,
    sort_order integer not null default 0,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    constraint specializations_fk_organization
        foreign key (organization_id) references organization (id) on delete cascade,
    constraint specializations_fk_department
        foreign key (department_id) references departments (id) on delete set null
);

create unique index if not exists ux_specializations_org_name on specializations (organization_id, name);
create index if not exists idx_specializations_organization_id on specializations (organization_id);
create index if not exists idx_specializations_department_id on specializations (department_id);

create table if not exists user_departments
(
    id character varying primary key,
    user_id character varying not null,
    department_id character varying not null,
    is_primary boolean not null default false,
    created_at timestamp without time zone not null default now(),
    constraint user_departments_fk_user
        foreign key (user_id) references users (id) on delete cascade,
    constraint user_departments_fk_department
        foreign key (department_id) references departments (id) on delete cascade
);

create unique index if not exists ux_user_departments_user_department on user_departments (user_id, department_id);
create index if not exists idx_user_departments_user_id on user_departments (user_id);
create index if not exists idx_user_departments_department_id on user_departments (department_id);

create table if not exists user_specializations
(
    id character varying primary key,
    user_id character varying not null,
    specialization_id character varying not null,
    is_primary boolean not null default false,
    created_at timestamp without time zone not null default now(),
    constraint user_specializations_fk_user
        foreign key (user_id) references users (id) on delete cascade,
    constraint user_specializations_fk_specialization
        foreign key (specialization_id) references specializations (id) on delete cascade
);

create unique index if not exists ux_user_specializations_user_specialization on user_specializations (user_id, specialization_id);
create index if not exists idx_user_specializations_user_id on user_specializations (user_id);
create index if not exists idx_user_specializations_specialization_id on user_specializations (specialization_id);
