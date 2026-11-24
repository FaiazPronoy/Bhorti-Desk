using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

// The children on a guardian's account. Only a student added here can apply.
[ApiController]
[Route("me/students")]
public class StudentsController(StudentService students) : ControllerBase
{
    [HttpGet]
    public async Task<List<StudentResponse>> List()
    {
        var guardian = Access.Guardian(HttpContext);
        return await students.ListForGuardianAsync(guardian.Id);
    }

    [HttpPost]
    public async Task<ActionResult<StudentResponse>> Add([FromBody] AddStudentRequest? request)
    {
        var guardian = Access.Guardian(HttpContext);
        var added = await students.AddAsync(guardian.Id, request);
        return Created("/me/students", added);
    }
}
