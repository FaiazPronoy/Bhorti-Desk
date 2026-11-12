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
