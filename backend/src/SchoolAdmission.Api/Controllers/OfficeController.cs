using System.Text;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

// School office only, always.
[ApiController]
[Route("office")]
public class OfficeController(OfficeService office) : ControllerBase
{
    [HttpGet("classes")]
    public Task<List<OfficeClassResponse>> Classes()
    {
        Access.Staff(HttpContext);
        return office.ClassesAsync();
    }

    [HttpPut("classes/{classId}")]
    public Task<OfficeClassResponse> Edit(string classId, [FromBody] ClassEditRequest? request) =>
        office.EditAsync(Access.Staff(HttpContext), ParseClassId(classId), request);

    [HttpPost("classes/{classId}/close")]
    public Task<OfficeClassResponse> Close(string classId) =>
        office.SetClosedAsync(Access.Staff(HttpContext), ParseClassId(classId), true);

    [HttpPost("classes/{classId}/reopen")]
    public Task<OfficeClassResponse> Reopen(string classId) =>
        office.SetClosedAsync(Access.Staff(HttpContext), ParseClassId(classId), false);

    [HttpGet("reports")]
    public Task<ReportResponse> Report()
    {
        Access.Staff(HttpContext);
        return office.ReportAsync();
    }

    [HttpGet("reports/classes.csv")]
    public async Task<IActionResult> ClassesCsv()
    {
        Access.Staff(HttpContext);
        return Csv(OfficeService.ClassesCsv(await office.ReportAsync()), "classes.csv");
    }

    [HttpGet("audit")]
    public Task<List<AuditEntry>> Audit([FromQuery] string? action, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        Access.Staff(HttpContext);
        return office.AuditAsync(action, from, to);
    }

    [HttpGet("audit.csv")]
    public async Task<IActionResult> AuditCsv([FromQuery] string? action, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        Access.Staff(HttpContext);
        return Csv(OfficeService.AuditCsv(await office.AuditAsync(action, from, to)), "audit-log.csv");
    }

    private FileContentResult Csv(string text, string name)
    {
        Response.Headers.CacheControl = "private, no-store";
        // A byte order mark so a spreadsheet opens Bangla class names correctly.
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(), "text/csv; charset=utf-8", name);
    }

    private static Guid ParseClassId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw AdmissionRuleException.ClassNotFound();
}
