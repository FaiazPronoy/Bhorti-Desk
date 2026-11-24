-- Runs at every start. Every statement is safe to run again on a database that already has the tables.

create table if not exists admission_classes (
    id          uuid         primary key,
    name        varchar(100) not null,
    total_seats integer      not null check (total_seats > 0),
    created_at  timestamptz  not null default clock_timestamp()
);

create table if not exists seat_holds (
    id                                uuid        primary key,
    admission_class_id                uuid        not null references admission_classes (id),
    student_birth_registration_number varchar(17) not null check (student_birth_registration_number ~ '^[0-9]{17}$'),
    status                            varchar(10) not null check (status in ('HELD', 'CONFIRMED', 'EXPIRED', 'CANCELLED')),
    expires_at                        timestamptz not null,
    created_at                        timestamptz not null default clock_timestamp(),
    confirmed_at                      timestamptz null,
    cancelled_at                      timestamptz null,
    expired_at                        timestamptz null
);

-- A student can have only one HELD or CONFIRMED application in a class.
-- The hold transaction checks this first; the index is the last line of defence.
create unique index if not exists ux_seat_holds_one_active_per_student_per_class
    on seat_holds (admission_class_id, student_birth_registration_number)
    where status in ('HELD', 'CONFIRMED');

create index if not exists ix_seat_holds_taken_seats_by_class
    on seat_holds (admission_class_id)
    where status in ('HELD', 'CONFIRMED');

create index if not exists ix_seat_holds_by_student
    on seat_holds (student_birth_registration_number, created_at desc);

-- ---------------------------------------------------------------- accounts

-- A guardian signs in with a mobile number and a one-time code.
create table if not exists guardians (
    id         uuid        primary key,
    phone      varchar(11) not null unique check (phone ~ '^01[3-9][0-9]{8}$'),
    full_name  varchar(100) null,
    created_at timestamptz not null default clock_timestamp()
);

-- School office staff sign in with an email and a password.
create table if not exists staff_users (
    id            uuid         primary key,
    email         varchar(254) not null unique,
    full_name     varchar(100) not null,
    password_hash text         not null,
    role          varchar(10)  not null check (role in ('ADMIN', 'OFFICER')),
    disabled      boolean      not null default false,
    created_at    timestamptz  not null default clock_timestamp()
);

-- Only a hash of each code is kept. A code works once, for five minutes, five tries.
create table if not exists otp_codes (
    id          uuid        primary key,
    phone       varchar(11) not null,
    code_hash   char(64)    not null,
    expires_at  timestamptz not null,
    attempts    integer     not null default 0,
    consumed_at timestamptz null,
    created_at  timestamptz not null default clock_timestamp()
);

create index if not exists ix_otp_codes_by_phone on otp_codes (phone, created_at desc);

-- Sign-in sessions. The browser holds a random token in an HttpOnly cookie; only its
-- SHA-256 is stored, so a copy of this table cannot be used to sign in.
create table if not exists user_sessions (
    token_hash   char(64)    primary key,
    user_kind    varchar(8)  not null check (user_kind in ('GUARDIAN', 'STAFF')),
    user_id      uuid        not null,
    created_at   timestamptz not null default clock_timestamp(),
    expires_at   timestamptz not null,
    revoked_at   timestamptz null
);

-- A birth registration number belongs to one guardian only, so nobody can apply in
-- another family's child's name.
create table if not exists students (
    id                         uuid         primary key,
    guardian_id                uuid         not null references guardians (id),
    birth_registration_number  varchar(17)  not null unique check (birth_registration_number ~ '^[0-9]{17}$'),
    full_name                  varchar(100) not null,
    date_of_birth              date         not null,
    gender                     varchar(6)   not null check (gender in ('MALE', 'FEMALE', 'OTHER')),
    created_at                 timestamptz  not null default clock_timestamp()
);

create index if not exists ix_students_by_guardian on students (guardian_id);

-- ---------------------------------------------------------------- admission season

-- An academic year's admission window. Only one can be published at a time.
create table if not exists admission_sessions (
    id            uuid         primary key,
    academic_year integer      not null check (academic_year between 2000 and 2100),
    name          varchar(100) not null,
    opens_at      timestamptz  not null,
    closes_at     timestamptz  not null,
    status        varchar(10)  not null default 'DRAFT' check (status in ('DRAFT', 'PUBLISHED', 'CLOSED')),
    created_at    timestamptz  not null default clock_timestamp(),
    check (closes_at > opens_at)
);

create unique index if not exists ux_admission_sessions_one_published
    on admission_sessions ((true))
    where status = 'PUBLISHED';

-- Null for a class made through the original POST /classes: it has no window and is
-- always open, exactly as the original contract says.
alter table admission_classes add column if not exists session_id uuid null references admission_sessions (id);

-- ---------------------------------------------------------------- audit log

-- Every office action, written in the same transaction as the change it records.
create table if not exists audit_log (
    id          bigint       generated always as identity primary key,
    actor_id    uuid         not null,
    actor_role  varchar(10)  not null,
    action      varchar(40)  not null,
    target_type varchar(20)  not null,
    target_id   varchar(40)  not null,
    detail      varchar(300) null,
    created_at  timestamptz  not null default clock_timestamp()
);

create index if not exists ix_audit_log_newest on audit_log (created_at desc);
-- ---------------------------------------------------------------- applicant details

-- A human serial for the token and slip. The hold id stays internal.
create sequence if not exists seat_hold_serials;
alter table seat_holds add column if not exists serial_number bigint not null default nextval('seat_hold_serials');
create unique index if not exists ux_seat_holds_serial on seat_holds (serial_number);

-- Rules the office sets for a class. A separate table, so the original class responses
-- and the original POST /classes never change.
create table if not exists class_rules (
    admission_class_id uuid    primary key references admission_classes (id),
    min_age            integer null check (min_age between 2 and 25),
    max_age            integer null check (max_age between 2 and 25),
    age_cutoff_date    date    null,
    fee_taka           integer not null default 0 check (fee_taka between 0 and 1000000),
    check (min_age is null or max_age is null or min_age <= max_age)
);

create table if not exists applicant_details (
    seat_hold_id    uuid         primary key references seat_holds (id),
    student_name_bn varchar(100) not null,
    student_name_en varchar(100) not null,
    date_of_birth   date         not null,
    guardian_name   varchar(100) not null,
    guardian_phone  varchar(11)  not null check (guardian_phone ~ '^01[3-9][0-9]{8}$'),
    previous_school varchar(150) null,
    updated_at      timestamptz  not null default clock_timestamp()
);
-- ---------------------------------------------------------------- documents

-- The file bytes live in the database, never under a web path, and get a random id.
-- The name the guardian's file had is not kept.
create table if not exists application_documents (
    id            uuid        primary key,
    seat_hold_id  uuid        not null references seat_holds (id),
    kind          varchar(12) not null check (kind in ('PHOTO', 'CERTIFICATE')),
    content_type  varchar(40) not null check (content_type in ('image/jpeg', 'image/png', 'application/pdf')),
    size_bytes    integer     not null check (size_bytes between 1 and 1048576),
    sha256        char(64)    not null,
    data          bytea       not null,
    created_at    timestamptz not null default clock_timestamp(),
    unique (seat_hold_id, kind)
);
-- ---------------------------------------------------------------- payments and review

-- One row per payment attempt. The idempotency key makes a double click or a repeated
-- request create the attempt once. Only the sandbox gateway exists.
create table if not exists payments (
    id              uuid         primary key,
    seat_hold_id    uuid         not null references seat_holds (id),
    amount_taka     integer      not null check (amount_taka >= 0),
    gateway         varchar(20)  not null check (gateway in ('SANDBOX')),
    status          varchar(10)  not null default 'PENDING' check (status in ('PENDING', 'PAID', 'FAILED', 'LATE')),
    idempotency_key varchar(80)  not null unique,
    created_at      timestamptz  not null default clock_timestamp(),
    finished_at     timestamptz  null
);

-- A hold can be paid once. A second callback finds this row and changes nothing.
create unique index if not exists ux_payments_one_paid_per_hold on payments (seat_hold_id) where status = 'PAID';

-- The office decision after payment. A rejection frees the seat (the hold becomes
-- CANCELLED, so it stops counting) and marks a refund as due. Nothing is paid back here.
create table if not exists application_reviews (
    seat_hold_id uuid         primary key references seat_holds (id),
    decision     varchar(10)  not null check (decision in ('VERIFIED', 'REJECTED')),
    reason       varchar(300) null,
    refund_due   boolean      not null default false,
    reviewer_id  uuid         not null,
    decided_at   timestamptz  not null default clock_timestamp()
);
-- ---------------------------------------------------------------- notices

-- One notice per event per application, so a repeated event never shows twice.
create table if not exists notices (
    id                                bigint      generated always as identity primary key,
    seat_hold_id                      uuid        not null references seat_holds (id),
    student_birth_registration_number varchar(17) not null,
    kind                              varchar(20) not null check (kind in ('HELD', 'TWO_MINUTES_LEFT', 'PAID', 'VERIFIED', 'REJECTED', 'EXPIRED', 'OFFERED')),
    created_at                        timestamptz not null default clock_timestamp(),
    sms_logged_at                     timestamptz null,
    unique (seat_hold_id, kind)
);

create index if not exists ix_notices_by_student on notices (student_birth_registration_number, created_at desc);