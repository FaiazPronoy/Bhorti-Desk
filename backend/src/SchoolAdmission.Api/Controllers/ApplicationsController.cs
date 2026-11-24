using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

// New endpoints beside the original eight. The owner is checked the same way as on confirm.
[ApiController]
[Route("applications")]
public class ApplicationsController(ApplicationService applications, StudentService students) : ControllerBase
{
    [HttpGet("{holdId}")]
    public async Task<ApplicationResponse> Get(string holdId)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return await applications.GetAsync(ParseHoldId(holdId), studentId);
    }

    [HttpPut("{holdId}/applicant")]
    public async Task<ApplicationResponse> SaveApplicant(string holdId, [FromBody] ApplicantRequest? request)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return await applications.SaveApplicantAsync(ParseHoldId(holdId), studentId, request);
    }

    private static Guid ParseHoldId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw AdmissionRuleException.HoldNotFound();
}

[ApiController]
public class ClassRulesController(ApplicationService applications) : ControllerBase
{
    // Fees and age rules are public: a guardian sees them before applying.
    [HttpGet("season/class-rules")]
    public Task<List<ClassRulesResponse>> List() => applications.ListRulesAsync();

    [HttpPut("office/classes/{classId}/rules")]
    public Task<ClassRulesResponse> Save(string classId, [FromBody] ClassRulesRequest? request)
    {
        var staff = Access.Staff(HttpContext);
        if (!Guid.TryParse(classId, out var id)) throw AdmissionRuleException.ClassNotFound();
        return applications.SaveRulesAsync(staff, id, request);
    }
}
