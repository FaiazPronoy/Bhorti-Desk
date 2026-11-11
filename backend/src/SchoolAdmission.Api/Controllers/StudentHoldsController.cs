using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me/holds")]
public class StudentHoldsController(SeatHoldService holdService) : ControllerBase
{
    // 8. GET /me/holds, with X-Student-Id. Newest first.
    [HttpGet]
    public async Task<List<StudentSeatHoldResponse>> List()
    {
        var studentId = StudentIdHeader.Read(Request);
        return await holdService.ListForStudentAsync(studentId);
    }
}
