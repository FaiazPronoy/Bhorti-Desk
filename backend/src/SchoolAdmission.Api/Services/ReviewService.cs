using System.Data;
using Dapper;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public class ReviewService(AdmissionDatabase database, Notices notices, ILogger<ReviewService> logger)
{
    private static readonly string OfficeSelectSql = $"""
        select h.id as hold_id, h.serial_number, h.created_at, h.admission_class_id as class_id, c.name as class_name,
               {SeatHoldStatus.EffectiveStatusSql} as status, right(h.student_birth_registration_number, 4) as last_four,
               a.student_name_bn, a.student_name_en, a.date_of_birth, a.guardian_name, a.guardian_phone, a.previous_school,
               coalesce((select string_agg(d.kind || ':' || d.content_type || ':' || d.size_bytes, ',' order by d.kind)
                           from application_documents d where d.seat_hold_id = h.id), '') as documents,
               p.id as payment_id, p.gateway, p.status as payment_status, p.amount_taka, p.finished_at,
               rv.decision, rv.reason, rv.refund_due
        from seat_holds h
        join admission_classes c on c.id = h.admission_class_id
        left join applicant_details a on a.seat_hold_id = h.id
        left join lateral (select * from payments p where p.seat_hold_id = h.id order by (p.status = 'PAID') desc, p.created_at desc limit 1) p on true
        left join application_reviews rv on rv.seat_hold_id = h.id
        """;

    // Paid applications, the ones waiting for a decision first.
    public async Task<List<OfficeApplicationResponse>> ListAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<OfficeRow>(OfficeSelectSql + """
             where h.status = 'CONFIRMED' or rv.decision is not null
             order by (rv.decision is null) desc, h.confirmed_at desc nulls last
             limit 200
            """);
        return rows.Select(row => row.ToResponse()).ToList();
    }

    public async Task<OfficeApplicationResponse> VerifyAsync(CurrentUser staff, Guid holdId)
    {
        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
        {
            await RequireUndecidedConfirmedAsync(connection, transaction, holdId);
            await connection.ExecuteAsync("""
                insert into application_reviews (seat_hold_id, decision, reviewer_id) values (@holdId, 'VERIFIED', @reviewer)
                """, new { holdId, reviewer = staff.Id }, transaction);
            await AuditLog.WriteAsync(connection, transaction, staff, "APPLICATION_VERIFIED", "HOLD", holdId.ToString());
            await notices.AddAsync(connection, transaction, holdId, Notices.Verified);
            await transaction.CommitAsync();
        }
        return await GetAsync(holdId);
    }

    // Rejecting frees the seat at once, under the same class lock as applying, so the
    // count can never be wrong for a moment. The paid fee is marked as due for refund.
    public async Task<OfficeApplicationResponse> RejectAsync(CurrentUser staff, Guid holdId, string? reason)
    {
        var cleanReason = (reason ?? "").Trim();
        if (cleanReason.Length is < 3 or > 300 || cleanReason.Any(char.IsControl))
        {
            throw new FieldValidationException(new() { ["reason"] = "review.error.reason" });
        }

        await using (var connection = await database.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
        {
            await connection.ExecuteAsync("set local lock_timeout = '20s'", transaction: transaction);
            await connection.ExecuteAsync("""
                select c.id from admission_classes c join seat_holds h on h.admission_class_id = c.id
                 where h.id = @holdId for update of c
                """, new { holdId }, transaction);
            await RequireUndecidedConfirmedAsync(connection, transaction, holdId);

            await connection.ExecuteAsync(
                "update seat_holds set status = 'CANCELLED', cancelled_at = clock_timestamp() where id = @holdId and status = 'CONFIRMED'",
                new { holdId }, transaction);
            var paid = await connection.ExecuteScalarAsync<bool>(
                "select exists (select 1 from payments where seat_hold_id = @holdId and status = 'PAID' and amount_taka > 0)", new { holdId }, transaction);
            await connection.ExecuteAsync("""
                insert into application_reviews (seat_hold_id, decision, reason, refund_due, reviewer_id)
                values (@holdId, 'REJECTED', @cleanReason, @paid, @reviewer)
                """, new { holdId, cleanReason, paid, reviewer = staff.Id }, transaction);
            await AuditLog.WriteAsync(connection, transaction, staff, "APPLICATION_REJECTED", "HOLD", holdId.ToString(), cleanReason);
            await notices.AddAsync(connection, transaction, holdId, Notices.Rejected);
            await transaction.CommitAsync();
        }
        logger.LogInformation("Application {HoldId} rejected, seat freed", holdId);
        return await GetAsync(holdId);
    }

    public async Task<OfficeApplicationResponse> GetAsync(Guid holdId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<OfficeRow>(OfficeSelectSql + " where h.id = @holdId", new { holdId });
        return row?.ToResponse() ?? throw AdmissionRuleException.HoldNotFound();
    }

    private static async Task RequireUndecidedConfirmedAsync(IDbConnection connection, IDbTransaction transaction, Guid holdId)
    {
        var state = await connection.QuerySingleOrDefaultAsync<(string Status, string? Decision)>("""
            select h.status, rv.decision from seat_holds h left join application_reviews rv on rv.seat_hold_id = h.id
             where h.id = @holdId for update of h
            """, new { holdId }, transaction);
        if (state.Status is null) throw AdmissionRuleException.HoldNotFound();
        if (state.Decision is not null) throw AdmissionRuleException.AlreadyReviewed();
        if (state.Status != SeatHoldStatus.Confirmed) throw AdmissionRuleException.NotPaidYet();
    }

    private class OfficeRow
    {
        public Guid HoldId { get; set; }
        public long SerialNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public string Status { get; set; } = "";
        public string LastFour { get; set; } = "";
        public string? StudentNameBn { get; set; }
        public string? StudentNameEn { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianPhone { get; set; }
        public string? PreviousSchool { get; set; }
        public string Documents { get; set; } = "";
        public Guid? PaymentId { get; set; }
        public string? Gateway { get; set; }
        public string? PaymentStatus { get; set; }
        public int? AmountTaka { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? Decision { get; set; }
        public string? Reason { get; set; }
        public bool? RefundDue { get; set; }

        public OfficeApplicationResponse ToResponse() => new(
            HoldId, Serials.Format(CreatedAt, SerialNumber), ClassId, ClassName, Status, LastFour,
            StudentNameBn is null ? null : new ApplicantResponse(StudentNameBn, StudentNameEn!, DateOnly.FromDateTime(DateOfBirth!.Value), GuardianName!, GuardianPhone!, PreviousSchool),
            Documents.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(item => item.Split(':')).Select(parts => new DocumentInfo(parts[0], parts[1], int.Parse(parts[2]))).ToArray(),
            PaymentId is null ? null : new PaymentInfo(PaymentId.Value, Gateway!, PaymentStatus!, AmountTaka ?? 0,
                FinishedAt is { } finished ? DateTime.SpecifyKind(finished.ToUniversalTime(), DateTimeKind.Utc) : null),
            Decision is null ? null : new ReviewInfo(Decision, Reason, RefundDue ?? false));
    }
}
