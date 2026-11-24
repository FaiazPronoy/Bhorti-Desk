using System.Data;
using Dapper;
using SchoolAdmission.Api.Configuration;

namespace SchoolAdmission.Api.Services;

// Gives freed seats to the people waiting, in the order they joined. Always called while
// the caller already holds the class row lock, so the seat count cannot change under it.
public class WaitlistOffers(AdmissionSettings settings, Notices notices)
{
    public async Task<int> OfferUnderLockAsync(IDbConnection connection, IDbTransaction transaction, Guid classId, int totalSeats)
    {
        var taken = await connection.ExecuteScalarAsync<long>("""
            select count(*) from seat_holds
             where admission_class_id = @classId
               and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
            """, new { classId }, transaction);
        var free = totalSeats - (int)taken;
        if (free <= 0) return 0;

        // The front of the line, oldest first. A person who already has a live seat in this
        // class is skipped and their place is closed.
        var next = (await connection.QueryAsync<(long Id, string StudentId)>("""
            select id, student_birth_registration_number from waitlist_entries
             where admission_class_id = @classId and status = 'WAITING'
             order by id
             limit @free
               for update
            """, new { classId, free }, transaction)).ToList();

        var offered = 0;
        foreach (var (entryId, studentId) in next)
        {
            var hasSeat = await connection.ExecuteScalarAsync<bool>("""
                select exists (select 1 from seat_holds where admission_class_id = @classId and student_birth_registration_number = @studentId
                                 and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp())))
                """, new { classId, studentId }, transaction);
            if (hasSeat)
            {
                await connection.ExecuteAsync("update waitlist_entries set status = 'LEFT' where id = @entryId", new { entryId }, transaction);
                continue;
            }

            var holdId = Guid.CreateVersion7();
            await connection.ExecuteAsync("""
                insert into seat_holds (id, admission_class_id, student_birth_registration_number, status, expires_at)
                values (@holdId, @classId, @studentId, 'HELD', date_trunc('second', clock_timestamp()) + make_interval(secs => @seconds))
                """, new { holdId, classId, studentId, seconds = settings.HoldDurationSeconds }, transaction);
            await connection.ExecuteAsync("""
                update waitlist_entries set status = 'OFFERED', offered_hold_id = @holdId, offered_at = clock_timestamp() where id = @entryId
                """, new { holdId, entryId }, transaction);
            await notices.AddAsync(connection, transaction, holdId, Notices.Offered);
            offered++;
        }
        return offered;
    }
}
