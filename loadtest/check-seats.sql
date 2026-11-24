-- Run after the load test. Every count must be 0.
select 'classes with more seats in use than they have' as rule, count(*) as broken
  from admission_classes c
 where (select count(*) from seat_holds h
         where h.admission_class_id = c.id
           and (h.status = 'CONFIRMED' or (h.status = 'HELD' and h.expires_at > clock_timestamp()))) > c.total_seats
union all
select 'students with two live seats in one class', count(*)
  from (select admission_class_id, student_birth_registration_number
          from seat_holds where status in ('HELD', 'CONFIRMED')
         group by 1, 2 having count(*) > 1) twice;

select c.name, c.total_seats,
       count(h.id) filter (where h.status = 'HELD' and h.expires_at > clock_timestamp()) as held,
       count(h.id) filter (where h.status = 'CONFIRMED') as confirmed
  from admission_classes c left join seat_holds h on h.admission_class_id = c.id
 group by c.id order by c.created_at;
