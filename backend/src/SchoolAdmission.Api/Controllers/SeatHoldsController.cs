using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public class SeatHoldsController(SeatHoldService holdService) : ControllerBase
{
    // 5. GET /holds/{holdId}. No header, as in the assignment.
    [HttpGet("{holdId}")]
    public async Task<SeatHoldStatusResponse> Get(string holdId)
    {
        return await holdService.GetHoldAsync(ParseHoldId(holdId));
    }

    // 6. POST /holds/{holdId}/confirm, with X-Student-Id. This is the "pay the fee" step.
    [HttpPost("{holdId}/confirm")]
    public async Task<SeatHoldConfirmedResponse> Confirm(string holdId)
    {
        var studentId = StudentIdHeader.Read(Request);
        return await holdService.ConfirmAsync(ParseHoldId(holdId), studentId);
    }

    // 7. DELETE /holds/{holdId}, with X-Student-Id.
    [HttpDelete("{holdId}")]
    public async Task<IActionResult> Cancel(string holdId)
    {
        var studentId = StudentIdHeader.Read(Request);
        await holdService.CancelAsync(ParseHoldId(holdId), studentId);
        return NoContent();
    }

    private static Guid ParseHoldId(string holdId) =>
        Guid.TryParse(holdId, out var id) ? id : throw AdmissionRuleException.HoldNotFound();
}
