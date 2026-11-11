using System.Data;
using Dapper;
using Npgsql;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public class SeatHoldService(AdmissionDatabase database, AdmissionSettings settings, ILogger<SeatHoldService> logger)
{
    // Every read of a hold goes through this select, so the status is always the effective one.
    private static readonly string HoldSelectSql = $"""
        select h.id as hold_id,
               h.admission_class_id as class_id,
               c.name as class_name,
               h.student_birth_registration_number as student_id,
               {SeatHoldStatus.EffectiveStatusSql} as status,
               h.expires_at,
               h.created_at,
               clock_timestamp() as server_time
        from seat_holds h
        join admission_classes c on c.id = h.admission_class_id
        """;

    // ---------------------------------------------------------------- endpoint 4

    public async Task<SeatHoldCreatedResponse> CreateHoldAsync(Guid classId, string studentId)
    {
        // A quick look before taking any lock. It can only say "full", never "free", so it
        // cannot break the seat limit. It keeps a crowd from queueing on a class that is
        // already full. The real check is the one inside the locked transaction below.
        // A student who already holds a seat here is told so even when the class is full.
        if (await IsClassFullAsync(classId, studentId))
        {
            throw AdmissionRuleException.ClassFull();
        }

        try
        {
            return await TryCreateHoldAsync(classId, studentId);
        }
        catch (PostgresException error) when (IsWorthOneRetry(error))
        {
            logger.LogWarning("Hold for class {ClassId} hit {SqlState}, trying once more", classId, error.SqlState);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The unique index caught a second active hold for this student and class.
            throw AdmissionRuleException.AlreadyApplied(null);
        }

        try
        {
            return await TryCreateHoldAsync(classId, studentId);
        }
        catch (PostgresException error) when (IsWorthOneRetry(error))
        {
            throw AdmissionRuleException.ServerBusy();
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw AdmissionRuleException.AlreadyApplied(null);
        }
    }

    private async Task<SeatHoldCreatedResponse> TryCreateHoldAsync(Guid classId, string studentId)
    {
        await using var connection = await database.OpenConnectionAsync();

        // Every statement below runs on this one connection inside this one transaction.
        // If the lock were taken outside the transaction it would end with that statement,
        // and two requests could both pass the seat count.
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        // "set local" only lasts until the end of this transaction, so the setting cannot
        // leak to the next request that gets this connection from the pool.
        await connection.ExecuteAsync("set local lock_timeout = '20s'", transaction: transaction);

        // 1. Lock the class row. Every hold request for this class now waits here in line,
        //    whichever backend process it came from. This is what keeps held + confirmed
        //    at or under total_seats, and it also works for two backends on one database.
        var totalSeats = await connection.QuerySingleOrDefaultAsync<int?>(
            "select total_seats from admission_classes where id = @classId for update",
            new { classId }, transaction);
        if (totalSeats is null)
        {
            throw AdmissionRuleException.ClassNotFound();
        }

        // 2. Mark this class's holds whose time has passed. Without this, the unique index
        //    in step 5 would block a student whose old hold expired from applying again.
        await connection.ExecuteAsync("""
            update seat_holds set status = 'EXPIRED', expired_at = clock_timestamp()
             where admission_class_id = @classId and status = 'HELD' and expires_at <= clock_timestamp()
            """, new { classId }, transaction);

        // 3. One active application or admission per student per class.
        var existingHoldId = await connection.QueryFirstOrDefaultAsync<Guid?>("""
            select id from seat_holds
             where admission_class_id = @classId and student_birth_registration_number = @studentId
               and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
            """, new { classId, studentId }, transaction);
        if (existingHoldId is not null)
        {
            throw AdmissionRuleException.AlreadyApplied(existingHoldId);
        }

        // 4. Count the taken seats while we still hold the lock.
        var takenSeats = await connection.ExecuteScalarAsync<long>("""
            select count(*) from seat_holds
             where admission_class_id = @classId
               and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
            """, new { classId }, transaction);
        if (takenSeats >= totalSeats)
        {
            throw AdmissionRuleException.ClassFull();
        }

        // 5. Take the seat. The expiry comes from the database clock, in whole seconds,
        //    so expiresAt prints like 2026-11-01T04:10:00Z.
        var holdId = Guid.CreateVersion7();
        var created = await connection.QuerySingleAsync<CreatedHoldRow>("""
            insert into seat_holds (id, admission_class_id, student_birth_registration_number, status, expires_at)
            values (@holdId, @classId, @studentId, 'HELD',
                    date_trunc('second', clock_timestamp()) + make_interval(secs => @holdSeconds))
            returning expires_at,
                      clock_timestamp() as server_time,
                      (select name from admission_classes where id = @classId) as class_name
            """, new { holdId, classId, studentId, holdSeconds = settings.HoldDurationSeconds }, transaction);

        await transaction.CommitAsync();

        logger.LogInformation("Hold created: {HoldId} in class {ClassId} for student {Student}",
            holdId, classId, StudentIdHeader.Masked(studentId));

        return new SeatHoldCreatedResponse(holdId, classId, studentId, SeatHoldStatus.Held,
            created.ExpiresAt, created.ClassName, created.ServerTime);
    }

    private async Task<bool> IsClassFullAsync(Guid classId, string studentId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<SeatCountRow>("""
            select c.total_seats,
                   (select count(*) from seat_holds h
                     where h.admission_class_id = c.id
                       and (h.status = 'CONFIRMED' or (h.status = 'HELD' and h.expires_at > clock_timestamp()))) as taken_seats,
                   exists (select 1 from seat_holds h
                            where h.admission_class_id = c.id
                              and h.student_birth_registration_number = @studentId
                              and (h.status = 'CONFIRMED' or (h.status = 'HELD' and h.expires_at > clock_timestamp()))) as student_has_seat
            from admission_classes c
            where c.id = @classId
            """, new { classId, studentId });
        // Unknown class, or a student who already has a seat: the transaction gives the
        // right answer (404 or ALREADY_APPLIED with the hold id).
        return row is not null && !row.StudentHasSeat && row.TakenSeats >= row.TotalSeats;
    }

    private static bool IsWorthOneRetry(PostgresException error) =>
        error.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected;

    // ---------------------------------------------------------------- endpoint 5

    public async Task<SeatHoldStatusResponse> GetHoldAsync(Guid holdId)
    {
        var hold = await ReadHoldAsync(holdId) ?? throw AdmissionRuleException.HoldNotFound();
        return new SeatHoldStatusResponse(hold.HoldId, hold.ClassId, hold.Status, hold.ExpiresAt,
            hold.ClassName, hold.CreatedAt, hold.ServerTime);
    }

    // ---------------------------------------------------------------- endpoint 6

    public async Task<SeatHoldConfirmedResponse> ConfirmAsync(Guid holdId, string studentId, bool isRetry = false)
    {
        int changedRows;
        await using (var connection = await database.OpenConnectionAsync())
        {
            // One statement, so there is nothing to lock. confirmedSeats is a count of
            // CONFIRMED rows, so confirming the same hold twice can only count it once.
            changedRows = await connection.ExecuteAsync("""
                update seat_holds set status = 'CONFIRMED', confirmed_at = clock_timestamp()
                 where id = @holdId and student_birth_registration_number = @studentId
                   and status = 'HELD' and expires_at > clock_timestamp()
                """, new { holdId, studentId });
        }

        if (changedRows == 1)
        {
            logger.LogInformation("Hold confirmed: {HoldId} by student {Student}", holdId, StudentIdHeader.Masked(studentId));
            return new SeatHoldConfirmedResponse(holdId, SeatHoldStatus.Confirmed);
        }

        // Nothing changed. Work out why, in the order: missing, not yours, then the status.
        var hold = await ReadOwnedHoldAsync(holdId, studentId);
        return hold.Status switch
        {
            SeatHoldStatus.Confirmed => new SeatHoldConfirmedResponse(holdId, SeatHoldStatus.Confirmed),
            SeatHoldStatus.Expired => throw AdmissionRuleException.Expired(),
            SeatHoldStatus.Cancelled => throw AdmissionRuleException.Cancelled(),
            // Still HELD although the update matched nothing: a race with the expiry
            // second. Try once more, then treat it as expired. Never a 500.
            _ when !isRetry => await ConfirmAsync(holdId, studentId, isRetry: true),
            _ => throw AdmissionRuleException.Expired(),
        };
    }

    // ---------------------------------------------------------------- endpoint 7

    public async Task CancelAsync(Guid holdId, string studentId, bool isRetry = false)
    {
        int changedRows;
        await using (var connection = await database.OpenConnectionAsync())
        {
            changedRows = await connection.ExecuteAsync("""
                update seat_holds set status = 'CANCELLED', cancelled_at = clock_timestamp()
                 where id = @holdId and student_birth_registration_number = @studentId
                   and status = 'HELD' and expires_at > clock_timestamp()
                """, new { holdId, studentId });
        }

        if (changedRows == 1)
        {
            logger.LogInformation("Hold cancelled: {HoldId} by student {Student}", holdId, StudentIdHeader.Masked(studentId));
            return;
        }

        var hold = await ReadOwnedHoldAsync(holdId, studentId);
        switch (hold.Status)
        {
            case SeatHoldStatus.Cancelled:
                return; // cancelling twice is fine
            case SeatHoldStatus.Confirmed:
                throw AdmissionRuleException.AlreadyConfirmed();
            case SeatHoldStatus.Expired:
                throw AdmissionRuleException.Expired();
            default:
                // Same race as in ConfirmAsync: try once more, then treat it as expired.
                if (!isRetry)
                {
                    await CancelAsync(holdId, studentId, isRetry: true);
                    return;
                }
                throw AdmissionRuleException.Expired();
        }
    }

    // ---------------------------------------------------------------- endpoint 8

    public async Task<List<StudentSeatHoldResponse>> ListForStudentAsync(string studentId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<HoldRow>(
            HoldSelectSql + " where h.student_birth_registration_number = @studentId order by h.created_at desc",
            new { studentId });
        return rows.Select(h => new StudentSeatHoldResponse(h.HoldId, h.ClassId, h.ClassName, h.Status,
            h.ExpiresAt, h.CreatedAt, h.ServerTime)).ToList();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<HoldRow?> ReadHoldAsync(Guid holdId)
    {
        await using var connection = await database.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<HoldRow>(HoldSelectSql + " where h.id = @holdId", new { holdId });
    }

    private async Task<HoldRow> ReadOwnedHoldAsync(Guid holdId, string studentId)
    {
        var hold = await ReadHoldAsync(holdId) ?? throw AdmissionRuleException.HoldNotFound();
        if (hold.StudentId != studentId)
        {
            throw AdmissionRuleException.NotOwned();
        }
        return hold;
    }

    private class HoldRow
    {
        public Guid HoldId { get; set; }
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ServerTime { get; set; }
    }

    private class SeatCountRow
    {
        public int TotalSeats { get; set; }
        public long TakenSeats { get; set; }
        public bool StudentHasSeat { get; set; }
    }

    private class CreatedHoldRow
    {
        public DateTime ExpiresAt { get; set; }
        public DateTime ServerTime { get; set; }
        public string ClassName { get; set; } = "";
    }
}
