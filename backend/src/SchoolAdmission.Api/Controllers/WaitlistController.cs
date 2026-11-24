using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
public class WaitlistController(WaitlistService waitlist, NoticeService notices, StudentService students) : ControllerBase
{
    [HttpPost("classes/{classId}/waitlist")]
    public async Task<ActionResult<WaitlistPlace>> Join(string classId)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        var place = await waitlist.JoinAsync(ParseClassId(classId), studentId);
        return Created("/me/waitlist", place);
    }

    [HttpDelete("classes/{classId}/waitlist")]
    public async Task<IActionResult> Leave(string classId)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        await waitlist.LeaveAsync(ParseClassId(classId), studentId);
        return NoContent();
    }

    [HttpGet("me/waitlist")]
    public async Task<List<WaitlistPlace>> Mine()
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return await waitlist.ListAsync(studentId);
    }

    [HttpGet("me/notices")]
    public async Task<List<NoticeResponse>> Notices()
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return await notices.ListAsync(studentId);
    }

    [HttpGet("metrics")]
    public Dictionary<string, long> Metrics() => SeasonMetrics.Snapshot();

    private static Guid ParseClassId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw AdmissionRuleException.ClassNotFound();
}
