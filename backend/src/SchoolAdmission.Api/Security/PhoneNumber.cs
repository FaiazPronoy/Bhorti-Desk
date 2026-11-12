namespace SchoolAdmission.Api.Security;

// Bangladeshi mobile numbers: 01XXXXXXXXX, with or without +880 or 880 in front.
public static class PhoneNumber
{
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("880")) digits = digits[2..];
        return digits.Length == 11 && digits.StartsWith("01") && digits[2] is >= '3' and <= '9' ? digits : null;
    }

    // For logs: only the last three digits.
    public static string Masked(string phone) => "********" + phone[^3..];
}
