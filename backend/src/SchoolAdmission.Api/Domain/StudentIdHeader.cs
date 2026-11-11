using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Domain;

// The only place that reads X-Student-Id. The value is the student's 17 digit birth
// registration number.
public static class StudentIdHeader
{
    public const string HeaderName = "X-Student-Id";

    public static string Read(HttpRequest request)
    {
        var raw = request.Headers[HeaderName].ToString().Trim();

        if (raw.Length == 0)
        {
            throw new AdmissionRuleException(StatusCodes.Status400BadRequest, ApiErrorCode.StudentIdRequired,
                "Send the student's birth registration number in the X-Student-Id header.");
        }

        // char.IsAsciiDigit, not char.IsDigit: IsDigit also accepts Bangla and other digits.
        if (raw.Length != 17 || !raw.All(char.IsAsciiDigit))
        {
            throw new AdmissionRuleException(StatusCodes.Status400BadRequest, ApiErrorCode.StudentIdInvalid,
                "The birth registration number must be exactly 17 digits.");
        }

        return raw;
    }

    // For logs: the full number is personal data, so only the last four digits are kept.
    public static string Masked(string studentId) => "*************" + studentId[^4..];
}
