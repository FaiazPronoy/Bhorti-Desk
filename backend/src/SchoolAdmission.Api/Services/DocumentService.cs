using System.Security.Cryptography;
using Dapper;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public record StoredDocument(string ContentType, byte[] Data);

public class DocumentService(AdmissionDatabase database, ILogger<DocumentService> logger)
{
    public async Task SaveAsync(Guid holdId, string studentId, string kind, IFormFile? file)
    {
        if (file is null || file.Length == 0) throw new FieldValidationException(new() { ["file"] = "document.error.missing" });
        if (file.Length > DocumentRules.MaxBytes(kind)) throw new FieldValidationException(new() { ["file"] = kind == DocumentRules.Photo ? "document.error.photoTooBig" : "document.error.certificateTooBig" });

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var data = buffer.ToArray();
        var contentType = DocumentRules.DetectContentType(kind, data)
            ?? throw new FieldValidationException(new() { ["file"] = kind == DocumentRules.Photo ? "document.error.photoType" : "document.error.certificateType" });

        await using var connection = await database.OpenConnectionAsync();
        var hold = await connection.QuerySingleOrDefaultAsync<(string StudentId, string Status)>($"""
            select h.student_birth_registration_number, {SeatHoldStatus.EffectiveStatusSql}
              from seat_holds h where h.id = @holdId
            """, new { holdId });
        if (hold.StudentId is null) throw AdmissionRuleException.HoldNotFound();
        if (hold.StudentId != studentId) throw AdmissionRuleException.NotOwned();
        if (hold.Status == SeatHoldStatus.Expired) throw AdmissionRuleException.Expired();
        if (hold.Status == SeatHoldStatus.Cancelled) throw AdmissionRuleException.Cancelled();

        await connection.ExecuteAsync("""
            insert into application_documents (id, seat_hold_id, kind, content_type, size_bytes, sha256, data)
            values (@id, @holdId, @kind, @contentType, @size, @sha, @data)
            on conflict (seat_hold_id, kind) do update
               set id = excluded.id, content_type = excluded.content_type, size_bytes = excluded.size_bytes,
                   sha256 = excluded.sha256, data = excluded.data, created_at = clock_timestamp()
            """, new { id = Guid.CreateVersion7(), holdId, kind, contentType, size = data.Length, sha = Convert.ToHexStringLower(SHA256.HashData(data)), data });
        logger.LogInformation("Document {Kind} saved for hold {HoldId}, {Size} bytes", kind, holdId, data.Length);
    }

    public async Task<StoredDocument> ReadForOwnerAsync(Guid holdId, string studentId, string kind)
    {
        await using var connection = await database.OpenConnectionAsync();
        var owner = await connection.ExecuteScalarAsync<string?>(
            "select student_birth_registration_number from seat_holds where id = @holdId", new { holdId });
        if (owner is null) throw AdmissionRuleException.HoldNotFound();
        if (owner != studentId) throw AdmissionRuleException.NotOwned();
        return await ReadAsync(connection, holdId, kind);
    }

    // Every look at a child's document by staff is written to the audit log.
    public async Task<StoredDocument> ReadForStaffAsync(CurrentUser staff, Guid holdId, string kind)
    {
        await using var connection = await database.OpenConnectionAsync();
        var document = await ReadAsync(connection, holdId, kind);
        await AuditLog.WriteAsync(connection, null, staff, "DOCUMENT_VIEWED", "HOLD", holdId.ToString(), kind);
        return document;
    }

    private static async Task<StoredDocument> ReadAsync(Npgsql.NpgsqlConnection connection, Guid holdId, string kind)
    {
        var row = await connection.QuerySingleOrDefaultAsync<(string ContentType, byte[] Data)>(
            "select content_type, data from application_documents where seat_hold_id = @holdId and kind = @kind", new { holdId, kind });
        return row.Data is null ? throw AdmissionRuleException.DocumentNotFound() : new StoredDocument(row.ContentType, row.Data);
    }
}
