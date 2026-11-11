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
