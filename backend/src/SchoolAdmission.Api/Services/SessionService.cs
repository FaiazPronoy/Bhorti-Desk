using Dapper;
using Npgsql;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public class SessionService(AdmissionDatabase database)
{
    // The window is always worked out by the database clock, never the browser's.
    private const string SessionSelectSql = """
        select s.id, s.academic_year, s.name, s.opens_at, s.closes_at, s.status,
               case when s.status = 'CLOSED' or s.closes_at <= clock_timestamp() then 'ENDED'
                    when s.opens_at > clock_timestamp() then 'UPCOMING'
                    else 'OPEN' end as window_state,
               coalesce((select array_agg(c.id order by c.created_at) from admission_classes c where c.session_id = s.id), '{}') as class_ids
        from admission_sessions s
        """;

    public async Task<CurrentSeasonResponse> CurrentAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(
            SessionSelectSql + " where s.status = 'PUBLISHED'");
        var serverTime = await connection.ExecuteScalarAsync<DateTime>("select clock_timestamp()");
        return new CurrentSeasonResponse(row?.ToResponse(), DateTime.SpecifyKind(serverTime.ToUniversalTime(), DateTimeKind.Utc));
    }

    public async Task<List<SessionResponse>> ListAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<SessionRow>(SessionSelectSql + " order by s.academic_year desc, s.created_at desc");
        return rows.Select(row => row.ToResponse()).ToList();
    }

    public async Task<SessionResponse> CreateAsync(CurrentUser staff, SessionRequest? request)
    {
        var form = Validate(request);
        var id = Guid.CreateVersion7();
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("""
            insert into admission_sessions (id, academic_year, name, opens_at, closes_at)
            values (@id, @Year, @Name, @OpensAt, @ClosesAt)
            """, new { id, form.Year, form.Name, form.OpensAt, form.ClosesAt }, transaction);
        await AuditLog.WriteAsync(connection, transaction, staff, "SESSION_CREATED", "SESSION", id.ToString(), form.Name);
        await transaction.CommitAsync();
        return await GetAsync(id);
    }

    public async Task<SessionResponse> UpdateAsync(CurrentUser staff, Guid id, SessionRequest? request)
    {
        var form = Validate(request);
        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var changed = await connection.ExecuteAsync("""
                update admission_sessions
                   set academic_year = @Year, name = @Name, opens_at = @OpensAt, closes_at = @ClosesAt
                 where id = @id
                """, new { id, form.Year, form.Name, form.OpensAt, form.ClosesAt }, transaction);
            if (changed == 0) throw AdmissionRuleException.SessionNotFound();
            await AuditLog.WriteAsync(connection, transaction, staff, "SESSION_UPDATED", "SESSION", id.ToString(), form.Name);
            await transaction.CommitAsync();
        }
        return await GetAsync(id);
    }

    public async Task<SessionResponse> SetStatusAsync(CurrentUser staff, Guid id, string status)
    {
        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            try
            {
                var changed = await connection.ExecuteAsync(
                    "update admission_sessions set status = @status where id = @id", new { id, status }, transaction);
                if (changed == 0) throw AdmissionRuleException.SessionNotFound();
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw AdmissionRuleException.SessionAlreadyPublished();
            }
            await AuditLog.WriteAsync(connection, transaction, staff, "SESSION_" + status, "SESSION", id.ToString());
            await transaction.CommitAsync();
        }
        return await GetAsync(id);
    }

    public async Task<SessionResponse> AttachClassAsync(CurrentUser staff, Guid id, Guid? classId)
    {
        if (classId is null) throw new FieldValidationException(new() { ["classId"] = "Choose a class." });
        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var sessionExists = await connection.ExecuteScalarAsync<bool>(
                "select exists (select 1 from admission_sessions where id = @id)", new { id }, transaction);
            if (!sessionExists) throw AdmissionRuleException.SessionNotFound();

            // The class row lock, so this waits for any apply in progress on the class.
            var changed = await connection.ExecuteAsync(
                "update admission_classes set session_id = @id where id = @classId", new { id, classId }, transaction);
            if (changed == 0) throw AdmissionRuleException.ClassNotFound();
            await AuditLog.WriteAsync(connection, transaction, staff, "SESSION_CLASS_ADDED", "CLASS", classId.Value.ToString(), id.ToString());
            await transaction.CommitAsync();
        }
        return await GetAsync(id);
    }

    private async Task<SessionResponse> GetAsync(Guid id)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(SessionSelectSql + " where s.id = @id", new { id });
        return row?.ToResponse() ?? throw AdmissionRuleException.SessionNotFound();
    }

    private static SessionForm Validate(SessionRequest? request)
    {
        var errors = new Dictionary<string, string>();
        var name = (request?.Name ?? "").Trim();
        if (name.Length is 0 or > 100 || name.Any(char.IsControl)) errors["name"] = "Write a session name, up to 100 characters.";
        if (request?.AcademicYear is not (>= 2000 and <= 2100)) errors["academicYear"] = "Enter the academic year, like 2027.";
        if (request?.OpensAt is null) errors["opensAt"] = "Choose when applications open.";
        if (request?.ClosesAt is null) errors["closesAt"] = "Choose when applications close.";
        else if (request.OpensAt is not null && request.ClosesAt <= request.OpensAt) errors["closesAt"] = "The closing time must be after the opening time.";
        if (errors.Count > 0) throw new FieldValidationException(errors);
        return new SessionForm(request!.AcademicYear!.Value, name, request.OpensAt!.Value.UtcDateTime, request.ClosesAt!.Value.UtcDateTime);
    }

    private record SessionForm(int Year, string Name, DateTime OpensAt, DateTime ClosesAt);

    private class SessionRow
    {
        public Guid Id { get; set; }
        public int AcademicYear { get; set; }
        public string Name { get; set; } = "";
        public DateTime OpensAt { get; set; }
        public DateTime ClosesAt { get; set; }
        public string Status { get; set; } = "";
        public string WindowState { get; set; } = "";
        public Guid[] ClassIds { get; set; } = [];

        public SessionResponse ToResponse() => new(Id, AcademicYear, Name, Utc(OpensAt), Utc(ClosesAt), Status, WindowState, ClassIds);

        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);
    }
}
