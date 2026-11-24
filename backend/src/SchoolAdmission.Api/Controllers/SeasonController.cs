using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

// The public view of the season: is admission open, and when.
[ApiController]
[Route("season")]
public class SeasonController(SessionService sessions) : ControllerBase
{
    [HttpGet("current")]
    public Task<CurrentSeasonResponse> Current() => sessions.CurrentAsync();
}

// School office only, whatever SIGN_IN_REQUIRED says: these are not part of the original contract.
[ApiController]
[Route("office/sessions")]
public class OfficeSessionsController(SessionService sessions) : ControllerBase
{
    [HttpGet]
    public Task<List<SessionResponse>> List()
    {
        Access.Staff(HttpContext);
        return sessions.ListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<SessionResponse>> Create([FromBody] SessionRequest? request)
    {
        var staff = Access.Staff(HttpContext);
        var created = await sessions.CreateAsync(staff, request);
        return Created($"/office/sessions/{created.Id}", created);
    }

    [HttpPut("{sessionId}")]
    public Task<SessionResponse> Update(string sessionId, [FromBody] SessionRequest? request) =>
        sessions.UpdateAsync(Access.Staff(HttpContext), ParseId(sessionId), request);

    [HttpPost("{sessionId}/publish")]
    public Task<SessionResponse> Publish(string sessionId) =>
        sessions.SetStatusAsync(Access.Staff(HttpContext), ParseId(sessionId), "PUBLISHED");

    [HttpPost("{sessionId}/close")]
    public Task<SessionResponse> Close(string sessionId) =>
        sessions.SetStatusAsync(Access.Staff(HttpContext), ParseId(sessionId), "CLOSED");

    [HttpPost("{sessionId}/classes")]
    public Task<SessionResponse> AttachClass(string sessionId, [FromBody] AttachClassRequest? request) =>
        sessions.AttachClassAsync(Access.Staff(HttpContext), ParseId(sessionId), request?.ClassId);

    private static Guid ParseId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw AdmissionRuleException.SessionNotFound();
}
