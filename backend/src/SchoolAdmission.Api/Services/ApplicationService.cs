using Dapper;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public class ApplicationService(AdmissionDatabase database)
{
    private static readonly string ApplicationSelectSql = $"""
        select h.id as hold_id, h.serial_number, h.created_at, h.admission_class_id as class_id, c.name as class_name,
               h.student_birth_registration_number as student_id,
               {SeatHoldStatus.EffectiveStatusSql} as status,
               h.expires_at, clock_timestamp() as server_time,
               r.min_age, r.max_age, r.age_cutoff_date, r.fee_taka, r.admission_class_id as rules_class_id,
               a.student_name_bn, a.student_name_en, a.date_of_birth, a.guardian_name, a.guardian_phone, a.previous_school,
               coalesce((select string_agg(d.kind || ':' || d.content_type || ':' || d.size_bytes, ',' order by d.kind)
                           from application_documents d where d.seat_hold_id = h.id), '') as documents
        from seat_holds h
        join admission_classes c on c.id = h.admission_class_id
        left join class_rules r on r.admission_class_id = c.id
        left join applicant_details a on a.seat_hold_id = h.id
        where h.id = @holdId
        """;

    public async Task<ApplicationResponse> GetAsync(Guid holdId, string studentId)
    {
        await using var connection = await database.OpenConnectionAsync();
        return (await ReadOwnedAsync(connection, holdId, studentId)).ToResponse();
    }

    public async Task<ApplicationResponse> SaveApplicantAsync(Guid holdId, string studentId, ApplicantRequest? request)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await ReadOwnedAsync(connection, holdId, studentId);
        if (row.Status == SeatHoldStatus.Expired) throw AdmissionRuleException.Expired();
        if (row.Status == SeatHoldStatus.Cancelled) throw AdmissionRuleException.Cancelled();
        if (row.Status == SeatHoldStatus.Confirmed) throw AdmissionRuleException.AlreadyConfirmed();

        var today = DateOnly.FromDateTime(row.ServerTime);
        var (clean, errors) = ApplicantRules.Check(request, studentId, row.ToResponse().Rules, today);
        if (clean is null) throw new FieldValidationException(errors);

        await connection.ExecuteAsync("""
            insert into applicant_details (seat_hold_id, student_name_bn, student_name_en, date_of_birth, guardian_name, guardian_phone, previous_school)
            values (@holdId, @StudentNameBn, @StudentNameEn, @birth, @GuardianName, @GuardianPhone, @PreviousSchool)
            on conflict (seat_hold_id) do update
               set student_name_bn = excluded.student_name_bn, student_name_en = excluded.student_name_en,
                   date_of_birth = excluded.date_of_birth, guardian_name = excluded.guardian_name,
                   guardian_phone = excluded.guardian_phone, previous_school = excluded.previous_school,
                   updated_at = clock_timestamp()
            """, new { holdId, clean.StudentNameBn, clean.StudentNameEn, birth = clean.DateOfBirth.ToDateTime(TimeOnly.MinValue), clean.GuardianName, clean.GuardianPhone, clean.PreviousSchool });

        return (await ReadOwnedAsync(connection, holdId, studentId)).ToResponse();
    }

    public async Task<List<ClassRulesResponse>> ListRulesAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<RulesRow>(
            "select admission_class_id as class_id, min_age, max_age, age_cutoff_date, fee_taka from class_rules");
        return rows.Select(row => row.ToResponse()).ToList();
    }

    public async Task<ClassRulesResponse> SaveRulesAsync(CurrentUser staff, Guid classId, ClassRulesRequest? request)
    {
        var errors = new Dictionary<string, string>();
        if (request?.MinAge is < 2 or > 25) errors["minAge"] = "office.rules.ageRange";
        if (request?.MaxAge is < 2 or > 25) errors["maxAge"] = "office.rules.ageRange";
        if (request?.MinAge is { } min && request.MaxAge is { } max && min > max) errors["maxAge"] = "office.rules.ageOrder";
        if ((request?.MinAge is not null || request?.MaxAge is not null) && request?.AgeCutoffDate is null) errors["ageCutoffDate"] = "office.rules.cutoff";
        if (request?.FeeTaka is null or < 0 or > 1_000_000) errors["feeTaka"] = "office.rules.fee";
        if (errors.Count > 0) throw new FieldValidationException(errors);

        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var exists = await connection.ExecuteScalarAsync<bool>(
            "select exists (select 1 from admission_classes where id = @classId)", new { classId }, transaction);
        if (!exists) throw AdmissionRuleException.ClassNotFound();

        var row = await connection.QuerySingleAsync<RulesRow>("""
            insert into class_rules (admission_class_id, min_age, max_age, age_cutoff_date, fee_taka)
            values (@classId, @MinAge, @MaxAge, @cutoff, @FeeTaka)
            on conflict (admission_class_id) do update
               set min_age = excluded.min_age, max_age = excluded.max_age,
                   age_cutoff_date = excluded.age_cutoff_date, fee_taka = excluded.fee_taka
            returning admission_class_id as class_id, min_age, max_age, age_cutoff_date, fee_taka
            """, new { classId, request!.MinAge, request.MaxAge, cutoff = request.AgeCutoffDate?.ToDateTime(TimeOnly.MinValue), request.FeeTaka }, transaction);
        await AuditLog.WriteAsync(connection, transaction, staff, "CLASS_RULES_SAVED", "CLASS", classId.ToString(), $"fee {request.FeeTaka}");
        await transaction.CommitAsync();
        return row.ToResponse();
    }

    private static async Task<ApplicationRow> ReadOwnedAsync(Npgsql.NpgsqlConnection connection, Guid holdId, string studentId)
    {
        var row = await connection.QuerySingleOrDefaultAsync<ApplicationRow>(ApplicationSelectSql, new { holdId })
            ?? throw AdmissionRuleException.HoldNotFound();
        if (row.StudentId != studentId) throw AdmissionRuleException.NotOwned();
        return row;
    }

    private class RulesRow
    {
        public Guid ClassId { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public DateTime? AgeCutoffDate { get; set; }
        public int FeeTaka { get; set; }

        public ClassRulesResponse ToResponse() =>
            new(ClassId, MinAge, MaxAge, AgeCutoffDate is { } date ? DateOnly.FromDateTime(date) : null, FeeTaka);
    }

    private class ApplicationRow
    {
        public Guid HoldId { get; set; }
        public long SerialNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public DateTime ServerTime { get; set; }
        public Guid? RulesClassId { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public DateTime? AgeCutoffDate { get; set; }
        public int? FeeTaka { get; set; }
        public string? StudentNameBn { get; set; }
        public string? StudentNameEn { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianPhone { get; set; }
        public string? PreviousSchool { get; set; }
        public string Documents { get; set; } = "";

        public ApplicationResponse ToResponse()
        {
            var rules = RulesClassId is null ? null : new ClassRulesResponse(ClassId, MinAge, MaxAge,
                AgeCutoffDate is { } cutoff ? DateOnly.FromDateTime(cutoff) : null, FeeTaka ?? 0);
            var applicant = StudentNameBn is null ? null : new ApplicantResponse(StudentNameBn, StudentNameEn!,
                DateOnly.FromDateTime(DateOfBirth!.Value), GuardianName!, GuardianPhone!, PreviousSchool);
            var serial = $"BD-{CreatedAt.ToUniversalTime().Year}-{SerialNumber:D7}";
            return new ApplicationResponse(HoldId, serial, ClassId, ClassName, Status,
                DateTime.SpecifyKind(ExpiresAt.ToUniversalTime(), DateTimeKind.Utc),
                DateTime.SpecifyKind(ServerTime.ToUniversalTime(), DateTimeKind.Utc), rules, applicant,
                Documents.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Split(':'))
                    .Select(parts => new DocumentInfo(parts[0], parts[1], int.Parse(parts[2])))
                    .ToArray());
        }
    }
}
