using Dapper;
using Npgsql;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Services;

public record NoticeResponse(long Id, Guid HoldId, string ClassName, string Kind, DateTime CreatedAt);

public class NoticeService(AdmissionDatabase database, ISmsSender sms, ILogger<NoticeService> logger)
{
    public async Task<List<NoticeResponse>> ListAsync(string studentId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<NoticeResponse>("""
            select n.id, n.seat_hold_id as hold_id, c.name as class_name, n.kind, n.created_at
              from notices n join seat_holds h on h.id = n.seat_hold_id join admission_classes c on c.id = h.admission_class_id
             where n.student_birth_registration_number = @studentId
             order by n.created_at desc, n.id desc
             limit 50
            """, new { studentId });
        return rows.Select(row => row with { CreatedAt = DateTime.SpecifyKind(row.CreatedAt.ToUniversalTime(), DateTimeKind.Utc) }).ToList();
    }

    // Notices that depend only on the clock: two minutes left, and time over.
    public async Task WriteTimedNoticesAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync("""
            insert into notices (seat_hold_id, student_birth_registration_number, kind)
            select id, student_birth_registration_number, 'TWO_MINUTES_LEFT' from seat_holds
             where status = 'HELD' and expires_at > clock_timestamp() and expires_at <= clock_timestamp() + interval '2 minutes'
            on conflict (seat_hold_id, kind) do nothing
            """);
        await connection.ExecuteAsync("""
            insert into notices (seat_hold_id, student_birth_registration_number, kind)
            select id, student_birth_registration_number, 'EXPIRED' from seat_holds
             where (status = 'HELD' or status = 'EXPIRED')
               and expires_at <= clock_timestamp() and expires_at > clock_timestamp() - interval '1 day'
               and not exists (select 1 from payments p where p.seat_hold_id = seat_holds.id and p.status = 'PAID')
            on conflict (seat_hold_id, kind) do nothing
            """);
    }

    // Hands each new notice to the SMS provider once. In demo mode the provider only logs.
    public async Task SendPendingSmsAsync(NpgsqlConnection connection)
    {
        var pending = (await connection.QueryAsync<(long Id, string Kind, string ClassName)>("""
            select n.id, n.kind, c.name from notices n
              join seat_holds h on h.id = n.seat_hold_id join admission_classes c on c.id = h.admission_class_id
             where n.sms_logged_at is null order by n.id limit 100
            """)).ToList();
        foreach (var (id, kind, className) in pending)
        {
            // The guardian's phone is only known for applications with details filled in.
            var phone = await connection.ExecuteScalarAsync<string?>("""
                select a.guardian_phone from notices n join applicant_details a on a.seat_hold_id = n.seat_hold_id where n.id = @id
                """, new { id });
            if (phone is not null) await sms.SendAsync(phone, $"Bhorti Desk: {className}: {kind}");
            await connection.ExecuteAsync("update notices set sms_logged_at = clock_timestamp() where id = @id", new { id });
        }
        if (pending.Count > 0) logger.LogInformation("Passed {Count} notices to the SMS provider", pending.Count);
    }
}
