using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
public class DocumentsController(DocumentService documents, StudentService students) : ControllerBase
{
    // The app limits every other body to 16 KB. Uploads get a little more than the largest file.
    [HttpPut("applications/{holdId}/documents/{kind}")]
    [RequestSizeLimit(DocumentRules.MaxCertificateBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(string holdId, string kind, IFormFile? file)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        await documents.SaveAsync(ParseHoldId(holdId), studentId, ParseKind(kind), file);
        return NoContent();
    }

    [HttpGet("applications/{holdId}/documents/{kind}")]
    public async Task<IActionResult> ReadOwn(string holdId, string kind)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return Send(await documents.ReadForOwnerAsync(ParseHoldId(holdId), studentId, ParseKind(kind)));
    }

    [HttpGet("office/applications/{holdId}/documents/{kind}")]
    public async Task<IActionResult> ReadAsStaff(string holdId, string kind)
    {
        var staff = Access.Staff(HttpContext);
        return Send(await documents.ReadForStaffAsync(staff, ParseHoldId(holdId), ParseKind(kind)));
    }

    // A child's document must never be kept in a shared cache or shown as a download page.
    private FileContentResult Send(StoredDocument document)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentDisposition = "inline";
        return File(document.Data, document.ContentType);
    }

    private static Guid ParseHoldId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw AdmissionRuleException.HoldNotFound();

    private static string ParseKind(string kind) =>
        DocumentRules.ParseKind(kind) ?? throw AdmissionRuleException.DocumentNotFound();
}
