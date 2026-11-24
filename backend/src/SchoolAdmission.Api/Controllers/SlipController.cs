using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
public class SlipController(SlipService slips, StudentService students) : ControllerBase
{
    // The QR points at the web app's verify page. PUBLIC_APP_URL sets it for a deployment;
    // otherwise the address the request came from is used.
    [HttpGet("applications/{holdId}/slip")]
    public async Task<SlipResponse> Slip(string holdId)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        if (!Guid.TryParse(holdId, out var id)) throw AdmissionRuleException.HoldNotFound();
        var appUrl = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["PUBLIC_APP_URL"];
        var origin = string.IsNullOrWhiteSpace(appUrl) ? Request.Headers.Origin.ToString() : appUrl;
        if (string.IsNullOrWhiteSpace(origin)) origin = $"{Request.Scheme}://{Request.Host}";
        return await slips.GetAsync(id, studentId, origin);
    }

    [HttpGet("verify/{code}")]
    public Task<VerifyResponse> Verify(string code) => slips.VerifyAsync(code);
}
