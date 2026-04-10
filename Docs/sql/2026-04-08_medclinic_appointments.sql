create table if not exists appointments
(
    id character varying primary key,
    organization_id character varying not null,
    patient_id character varying null,
    doctor_id character varying null,
    title character varying null,
    phone character varying null,
    email character varying null,
    notes text null,
    referral_source character varying null,
    payment_type character varying null,
    appointment_status character varying null,
    is_active boolean not null default true,
    starts_at timestamp without time zone not null,
    ends_at timestamp without time zone not null,
    created_at timestamp without time zone not null default now(),
    updated_at timestamp without time zone not null default now(),
    created_by character varying null,
    constraint appointments_fk_organization
        foreign key (organization_id) references organization (id) on delete cascade,
    constraint appointments_fk_patient
        foreign key (patient_id) references organization_clients (id) on delete set null,
    constraint appointments_fk_doctor
        foreign key (doctor_id) references users (id) on delete set null
);

create index if not exists idx_appointments_organization_id on appointments (organization_id);
create index if not exists idx_appointments_patient_id on appointments (patient_id);
create index if not exists idx_appointments_doctor_id on appointments (doctor_id);
create index if not exists idx_appointments_starts_at on appointments (starts_at);

create table if not exists appointment_services
(
    id character varying primary key,
    appointment_id character varying not null,
    organization_service_id character varying null,
    service_name character varying null,
    price_tyiyn numeric(18,2) not null default 0,
    quantity integer not null default 1,
    constraint appointment_services_fk_appointment
        foreign key (appointment_id) references appointments (id) on delete cascade,
    constraint appointment_services_fk_org_service
        foreign key (organization_service_id) references organization_services (id) on delete set null
);

create index if not exists idx_appointment_services_appointment_id on appointment_services (appointment_id);
create index if not exists idx_appointment_services_org_service_id on appointment_services (organization_service_id);

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
