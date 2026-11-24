using System.Data;
using System.Globalization;
using System.Text;
using Dapper;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public record ClassEditRequest(string? Name, int? TotalSeats);

public record OfficeClassResponse(Guid Id, string Name, int TotalSeats, int InUse, bool Closed, int Waiting);

public record HourCount(DateTime Hour, int Applications);

public record Funnel(int Applied, int OnHoldNow, int Paid, int Verified, int Rejected);

public record ReportResponse(List<HourCount> ApplicationsPerHour, Funnel Funnel, List<OfficeClassResponse> Classes, int SandboxRevenueTaka);

public record AuditEntry(long Id, DateTime CreatedAt, string ActorRole, string Action, string TargetType, string TargetId, string? Detail);

public class OfficeService(AdmissionDatabase database, WaitlistOffers offers)
{
    private const string ClassesSql = """
        select c.id, c.name, c.total_seats,
               (select count(*) from seat_holds h where h.admission_class_id = c.id
                  and (h.status = 'CONFIRMED' or (h.status = 'HELD' and h.expires_at > clock_timestamp()))) as in_use,
               c.closed_at is not null as closed,
               (select count(*) from waitlist_entries w where w.admission_class_id = c.id and w.status = 'WAITING') as waiting
          from admission_classes c
        """;

    public async Task<List<OfficeClassResponse>> ClassesAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        return (await connection.QueryAsync<ClassRow>(ClassesSql + " order by c.created_at, c.id")).Select(row => row.ToResponse()).ToList();
    }

    // Seats can go down, but never below the seats already held or confirmed: those
    // families keep their places. Checked under the same class lock as applying.
    public async Task<OfficeClassResponse> EditAsync(CurrentUser staff, Guid classId, ClassEditRequest? request)
    {
        var name = (request?.Name ?? "").Trim();
        var errors = new Dictionary<string, string>();
        if (name.Length is 0 or > 100 || name.Any(char.IsControl)) errors["name"] = "office.edit.name";
        if (request?.TotalSeats is not (>= 1 and <= 100_000)) errors["totalSeats"] = "office.edit.seats";
        if (errors.Count > 0) throw new FieldValidationException(errors);

        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
        {
            await connection.ExecuteAsync("set local lock_timeout = '20s'", transaction: transaction);
            var exists = await connection.ExecuteScalarAsync<bool>(
                "select true from admission_classes where id = @classId for update", new { classId }, transaction);
            if (!exists) throw AdmissionRuleException.ClassNotFound();

            var inUse = await connection.ExecuteScalarAsync<long>("""
                select count(*) from seat_holds where admission_class_id = @classId
                   and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
                """, new { classId }, transaction);
            if (request!.TotalSeats!.Value < inUse)
            {
                throw new FieldValidationException(new() { ["totalSeats"] = "office.edit.belowInUse" });
            }

            await connection.ExecuteAsync("update admission_classes set name = @name, total_seats = @seats where id = @classId",
                new { classId, name, seats = request.TotalSeats.Value }, transaction);
            // More seats may mean someone in line gets one now.
            await offers.OfferUnderLockAsync(connection, transaction, classId, request.TotalSeats.Value);
            await AuditLog.WriteAsync(connection, transaction, staff, "CLASS_EDITED", "CLASS", classId.ToString(), $"{name}, {request.TotalSeats} seats");
            await transaction.CommitAsync();
        }
        return await ClassAsync(classId);
    }

    public async Task<OfficeClassResponse> SetClosedAsync(CurrentUser staff, Guid classId, bool closed)
    {
        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var changed = await connection.ExecuteAsync(
                closed
                    ? "update admission_classes set closed_at = coalesce(closed_at, clock_timestamp()) where id = @classId"
                    : "update admission_classes set closed_at = null where id = @classId",
                new { classId }, transaction);
            if (changed == 0) throw AdmissionRuleException.ClassNotFound();
            await AuditLog.WriteAsync(connection, transaction, staff, closed ? "CLASS_CLOSED" : "CLASS_REOPENED", "CLASS", classId.ToString());
            await transaction.CommitAsync();
        }
        return await ClassAsync(classId);
    }

    public async Task<ReportResponse> ReportAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var perHour = (await connection.QueryAsync<(DateTime Hour, long Count)>("""
            select date_trunc('hour', created_at) as hour, count(*) from seat_holds
             where created_at > clock_timestamp() - interval '24 hours'
             group by 1 order by 1
            """)).Select(row => new HourCount(DateTime.SpecifyKind(row.Hour.ToUniversalTime(), DateTimeKind.Utc), (int)row.Count)).ToList();
        var funnel = await connection.QuerySingleAsync<(long Applied, long OnHold, long Paid, long Verified, long Rejected)>("""
            select (select count(*) from seat_holds),
                   (select count(*) from seat_holds where status = 'HELD' and expires_at > clock_timestamp()),
                   (select count(*) from seat_holds h where h.status = 'CONFIRMED' or exists (select 1 from application_reviews r where r.seat_hold_id = h.id)),
                   (select count(*) from application_reviews where decision = 'VERIFIED'),
                   (select count(*) from application_reviews where decision = 'REJECTED')
            """);
        var revenue = await connection.ExecuteScalarAsync<long>("select coalesce(sum(amount_taka), 0) from payments where status = 'PAID'");
        var classes = (await connection.QueryAsync<ClassRow>(ClassesSql + " order by c.created_at, c.id")).Select(row => row.ToResponse()).ToList();
        return new ReportResponse(perHour, new Funnel((int)funnel.Applied, (int)funnel.OnHold, (int)funnel.Paid, (int)funnel.Verified, (int)funnel.Rejected), classes, (int)revenue);
    }

    public async Task<List<AuditEntry>> AuditAsync(string? action, DateTimeOffset? from, DateTimeOffset? to)
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<AuditEntry>("""
            select id, created_at, actor_role, action, target_type, target_id, detail from audit_log
             where (cast(@action as text) is null or action = cast(@action as text))
               and (cast(@from as timestamptz) is null or created_at >= cast(@from as timestamptz))
               and (cast(@to as timestamptz) is null or created_at < cast(@to as timestamptz))
             order by id desc limit 500
            """, new { action = string.IsNullOrWhiteSpace(action) ? null : action.Trim().ToUpperInvariant(), from = from?.UtcDateTime, to = to?.UtcDateTime });
        return rows.Select(row => row with { CreatedAt = DateTime.SpecifyKind(row.CreatedAt.ToUniversalTime(), DateTimeKind.Utc) }).ToList();
    }

    public static string ClassesCsv(ReportResponse report)
    {
        var csv = new StringBuilder("class,total_seats,in_use,fill_percent,waiting,closed\n");
        foreach (var item in report.Classes)
        {
            var fill = item.TotalSeats == 0 ? 0 : Math.Round(100.0 * item.InUse / item.TotalSeats, 1);
            csv.Append(Field(item.Name)).Append(',').Append(item.TotalSeats).Append(',').Append(item.InUse).Append(',')
               .Append(fill.ToString(CultureInfo.InvariantCulture)).Append(',').Append(item.Waiting).Append(',').Append(item.Closed ? "yes" : "no").Append('\n');
        }
        return csv.ToString();
    }

    public static string AuditCsv(List<AuditEntry> entries)
    {
        var csv = new StringBuilder("time_utc,role,action,target_type,target_id,detail\n");
        foreach (var entry in entries)
        {
            csv.Append(entry.CreatedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append(',').Append(Field(entry.ActorRole)).Append(',')
               .Append(Field(entry.Action)).Append(',').Append(Field(entry.TargetType)).Append(',').Append(Field(entry.TargetId)).Append(',')
               .Append(Field(entry.Detail ?? "")).Append('\n');
        }
        return csv.ToString();
    }

    // Quotes a CSV field, and stops a class name like "=HYPERLINK(...)" from running as a
    // formula when the office opens the file in a spreadsheet.
    private static string Field(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private async Task<OfficeClassResponse> ClassAsync(Guid classId)
    {
        await using var connection = await database.OpenConnectionAsync();
        return (await connection.QuerySingleAsync<ClassRow>(ClassesSql + " where c.id = @classId", new { classId })).ToResponse();
    }

    private class ClassRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int TotalSeats { get; set; }
        public long InUse { get; set; }
        public bool Closed { get; set; }
        public long Waiting { get; set; }

        public OfficeClassResponse ToResponse() => new(Id, Name, TotalSeats, (int)InUse, Closed, (int)Waiting);
    }
}
