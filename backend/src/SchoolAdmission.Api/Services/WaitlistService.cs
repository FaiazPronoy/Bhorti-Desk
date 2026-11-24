using System.Data;
using Dapper;
using Npgsql;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public record WaitlistPlace(long EntryId, Guid ClassId, string ClassName, string Status, int Position, Guid? OfferedHoldId);

public class WaitlistService(AdmissionDatabase database, WaitlistOffers offers, ILogger<WaitlistService> logger)
{
    // Join the line. Only when the class is really full: with a free seat, apply instead.
    public async Task<WaitlistPlace> JoinAsync(Guid classId, string studentId)
    {
        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
        {
            await connection.ExecuteAsync("set local lock_timeout = '20s'", transaction: transaction);
            var totalSeats = await connection.QuerySingleOrDefaultAsync<int?>(
                "select total_seats from admission_classes where id = @classId for update", new { classId }, transaction)
                ?? throw AdmissionRuleException.ClassNotFound();

            // People already waiting get freed seats first.
            await offers.OfferUnderLockAsync(connection, transaction, classId, totalSeats);

            var active = await connection.ExecuteScalarAsync<Guid?>("""
                select id from seat_holds where admission_class_id = @classId and student_birth_registration_number = @studentId
                   and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
                """, new { classId, studentId }, transaction);
            if (active is not null) throw AdmissionRuleException.AlreadyApplied(active);

            var taken = await connection.ExecuteScalarAsync<long>("""
                select count(*) from seat_holds where admission_class_id = @classId
                   and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
                """, new { classId }, transaction);
            if (taken < totalSeats) throw AdmissionRuleException.SeatsStillFree();

            try
            {
                await connection.ExecuteAsync("""
                    insert into waitlist_entries (admission_class_id, student_birth_registration_number) values (@classId, @studentId)
                    """, new { classId, studentId }, transaction);
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw AdmissionRuleException.AlreadyWaiting();
            }
            await transaction.CommitAsync();
        }
        logger.LogInformation("Student {Student} joined the waiting list of class {ClassId}", Domain.StudentIdHeader.Masked(studentId), classId);
        return (await ListAsync(studentId)).First(place => place.ClassId == classId && place.Status == "WAITING");
    }

    public async Task LeaveAsync(Guid classId, string studentId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            update waitlist_entries set status = 'LEFT'
             where admission_class_id = @classId and student_birth_registration_number = @studentId and status = 'WAITING'
            """, new { classId, studentId });
    }

    // Also the "next request" that makes offers if the worker was asleep: before it
    // answers, it gives out any free seat in the classes this student waits for.
    public async Task<List<WaitlistPlace>> ListAsync(string studentId)
    {
        await using (var connection = await database.OpenConnectionAsync())
        {
            var classIds = (await connection.QueryAsync<Guid>("""
                select distinct admission_class_id from waitlist_entries where student_birth_registration_number = @studentId and status = 'WAITING'
                """, new { studentId })).ToList();
            foreach (var classId in classIds) await OfferForClassAsync(connection, classId);
        }

        await using var reader = await database.OpenConnectionAsync();
        var rows = await reader.QueryAsync<WaitlistRow>("""
            select w.id as entry_id, w.admission_class_id as class_id, c.name as class_name, w.status, w.offered_hold_id,
                   (select count(*) from waitlist_entries ahead
                     where ahead.admission_class_id = w.admission_class_id and ahead.status = 'WAITING' and ahead.id <= w.id) as position
              from waitlist_entries w join admission_classes c on c.id = w.admission_class_id
             where w.student_birth_registration_number = @studentId and w.status in ('WAITING', 'OFFERED')
             order by w.id desc
            """, new { studentId });
        return rows.Select(row => new WaitlistPlace(row.EntryId, row.ClassId, row.ClassName, row.Status, (int)row.Position, row.OfferedHoldId)).ToList();
    }

    // Used by the background worker and by ListAsync. One short transaction per class.
    public async Task<int> OfferForClassAsync(NpgsqlConnection connection, Guid classId)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await connection.ExecuteAsync("set local lock_timeout = '5s'", transaction: transaction);
        var totalSeats = await connection.QuerySingleOrDefaultAsync<int?>(
            "select total_seats from admission_classes where id = @classId for update", new { classId }, transaction);
        var offered = totalSeats is null ? 0 : await offers.OfferUnderLockAsync(connection, transaction, classId, totalSeats.Value);
        await transaction.CommitAsync();
        return offered;
    }

    private class WaitlistRow
    {
        public long EntryId { get; set; }
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public string Status { get; set; } = "";
        public Guid? OfferedHoldId { get; set; }
        public long Position { get; set; }
    }
}
