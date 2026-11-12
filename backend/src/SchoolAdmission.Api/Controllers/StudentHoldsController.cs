using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me/holds")]
public class StudentHoldsController(SeatHoldService holdService, StudentService students) : ControllerBase
{
    // 8. GET /me/holds, with X-Student-Id. Newest first.
    [HttpGet]
    public async Task<List<StudentSeatHoldResponse>> List()
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return await holdService.ListForStudentAsync(studentId);
    }
}
