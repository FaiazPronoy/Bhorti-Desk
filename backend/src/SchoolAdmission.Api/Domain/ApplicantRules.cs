using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Security;

namespace SchoolAdmission.Api.Domain;

// Checks the applicant form. Each error value is a message key, so the web app can show
// it in Bangla or English.
public static class ApplicantRules
{
    public static (ApplicantResponse? Clean, Dictionary<string, string> Errors) Check(
        ApplicantRequest? request, string studentId, ClassRulesResponse? rules, DateOnly today)
    {
        var errors = new Dictionary<string, string>();
        var nameBn = (request?.StudentNameBn ?? "").Trim();
        var nameEn = (request?.StudentNameEn ?? "").Trim();
        var guardian = (request?.GuardianName ?? "").Trim();
        var school = string.IsNullOrWhiteSpace(request?.PreviousSchool) ? null : request.PreviousSchool.Trim();
        var phone = PhoneNumber.Normalize(request?.GuardianPhone);

        if (!IsName(nameBn) || !nameBn.Any(IsBanglaLetter)) errors["studentNameBn"] = "applicant.error.nameBn";
        if (!IsName(nameEn) || !nameEn.All(c => char.IsAsciiLetter(c) || c is ' ' or '.' or '-' or '\'')) errors["studentNameEn"] = "applicant.error.nameEn";
        if (!IsName(guardian)) errors["guardianName"] = "applicant.error.guardianName";
        if (phone is null) errors["guardianPhone"] = "applicant.error.phone";
        if (school is { Length: > 150 } || (school?.Any(char.IsControl) ?? false)) errors["previousSchool"] = "applicant.error.school";

        if (request?.DateOfBirth is not { } birth || birth > today)
        {
            errors["dateOfBirth"] = "applicant.error.birthDate";
        }
        else if (studentId[..4] != birth.Year.ToString("D4"))
        {
            // A birth registration number starts with the year of birth.
            errors["dateOfBirth"] = "applicant.error.birthYear";
        }
        else if (rules?.AgeCutoffDate is { } cutoff)
        {
            var age = AgeOn(birth, cutoff);
            if (rules.MinAge is { } min && age < min) errors["dateOfBirth"] = "applicant.error.tooYoung";
            else if (rules.MaxAge is { } max && age > max) errors["dateOfBirth"] = "applicant.error.tooOld";
        }

        if (errors.Count > 0) return (null, errors);
        return (new ApplicantResponse(nameBn, nameEn, request!.DateOfBirth!.Value, guardian, phone!, school), errors);
    }

    // Completed years on the given date. Someone born on 29 February turns a year older
    // on 1 March in a year with no 29 February.
    public static int AgeOn(DateOnly birth, DateOnly date)
    {
        var age = date.Year - birth.Year;
        var birthdayNotReached = date.Month < birth.Month || (date.Month == birth.Month && date.Day < birth.Day);
        return birthdayNotReached ? age - 1 : age;
    }

    private static bool IsName(string value) => value.Length is >= 2 and <= 100 && !value.Any(char.IsControl);

    private static bool IsBanglaLetter(char c) => c is >= 'ঀ' and <= '৿';
}
