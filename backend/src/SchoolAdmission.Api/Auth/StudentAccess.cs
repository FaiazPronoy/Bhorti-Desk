using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Auth;

public static class StudentAccess
{
    // The signed-in guardian must own the student named in X-Student-Id. The header is
    // checked for format first, so a typo still gets its own clear 400.
    public static async Task RequireOwnAsync(HttpContext context, StudentService students, string studentId)
    {
        var guardian = Access.Guardian(context);
        if (!await students.GuardianOwnsAsync(guardian.Id, studentId))
        {
            throw AdmissionRuleException.StudentNotYours();
        }
    }
}
