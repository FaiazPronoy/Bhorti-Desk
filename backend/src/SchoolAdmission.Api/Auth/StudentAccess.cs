using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Auth;

public static class StudentAccess
{
    // With SIGN_IN_REQUIRED on, the signed-in guardian must own the student named in
    // X-Student-Id. With it off (the default) the header alone is the identity, as the
    // original contract says. The header is checked for format before this runs, so a
    // typo still gets its own clear 400.
    public static async Task RequireOwnAsync(HttpContext context, StudentService students, string studentId)
    {
        var settings = context.RequestServices.GetRequiredService<AdmissionSettings>();
        if (!settings.SignInRequired) return;

        var guardian = Access.Guardian(context);
        if (!await students.GuardianOwnsAsync(guardian.Id, studentId))
        {
            throw AdmissionRuleException.StudentNotYours();
        }
    }
}
