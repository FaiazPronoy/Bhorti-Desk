using Dapper;
using QRCoder;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Security;

namespace SchoolAdmission.Api.Services;

public record SlipResponse(
    Guid HoldId,
    string Serial,
    string ClassName,
    string MaskedStudentId,
    string AdmissionStatus,
    string? StudentNameBn,
    string? StudentNameEn,
    bool HasPhoto,
    string VerifyCode,
    // The QR as rows of dark (true) and light (false) modules. The page draws them as
    // SVG squares, so no markup from the server is ever put into the page.
    bool[][] Qr);

public record VerifyResponse(string ClassName, string MaskedStudentId, string AdmissionStatus);

public class SlipService(AdmissionDatabase database, SlipCodes codes)
{
    // PAID while waiting for the office, VERIFIED or REJECTED after the decision.
    private const string AdmissionStatusSql = """
        case when rv.decision is not null then rv.decision
             when h.status = 'CONFIRMED' then 'PAID'
             else 'NOT_ADMITTED' end
        """;

    public async Task<SlipResponse> GetAsync(Guid holdId, string studentId, string publicBaseUrl)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<SlipRow>($"""
            select h.id as hold_id, h.serial_number, h.created_at, c.name as class_name,
                   h.student_birth_registration_number as student_id, {AdmissionStatusSql} as admission_status,
                   a.student_name_bn, a.student_name_en,
                   exists (select 1 from application_documents d where d.seat_hold_id = h.id and d.kind = 'PHOTO') as has_photo
              from seat_holds h
              join admission_classes c on c.id = h.admission_class_id
              left join applicant_details a on a.seat_hold_id = h.id
              left join application_reviews rv on rv.seat_hold_id = h.id
             where h.id = @holdId
            """, new { holdId }) ?? throw AdmissionRuleException.HoldNotFound();
        if (row.StudentId != studentId) throw AdmissionRuleException.NotOwned();
        if (row.AdmissionStatus == "NOT_ADMITTED") throw AdmissionRuleException.NotPaidYet();

        var code = codes.Create(holdId);
        return new SlipResponse(holdId, Serials.Format(row.CreatedAt, row.SerialNumber), row.ClassName, Mask(studentId),
            row.AdmissionStatus, row.StudentNameBn, row.StudentNameEn, row.HasPhoto, code, QrModules($"{publicBaseUrl.TrimEnd('/')}/verify/{code}"));
    }

    // Public: shows only what a school gate needs to check a slip.
    public async Task<VerifyResponse> VerifyAsync(string code)
    {
        var holdId = codes.Read(code) ?? throw AdmissionRuleException.SlipInvalid();
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<(string ClassName, string StudentId, string AdmissionStatus)>($"""
            select c.name, h.student_birth_registration_number, {AdmissionStatusSql}
              from seat_holds h join admission_classes c on c.id = h.admission_class_id
              left join application_reviews rv on rv.seat_hold_id = h.id
             where h.id = @holdId
            """, new { holdId });
        if (row.ClassName is null) throw AdmissionRuleException.SlipInvalid();
        return new VerifyResponse(row.ClassName, Mask(row.StudentId), row.AdmissionStatus);
    }

    private static string Mask(string studentId) => new string('•', 13) + studentId[^4..];

    private static bool[][] QrModules(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return data.ModuleMatrix.Select(row => row.Cast<bool>().ToArray()).ToArray();
    }

    private class SlipRow
    {
        public Guid HoldId { get; set; }
        public long SerialNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ClassName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public string AdmissionStatus { get; set; } = "";
        public string? StudentNameBn { get; set; }
        public string? StudentNameEn { get; set; }
        public bool HasPhoto { get; set; }
    }
}
