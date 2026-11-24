namespace SchoolAdmission.Api.Domain;

public static class Serials
{
    // BD-2026-0001234: the year the application was made, then the number from the sequence.
    public static string Format(DateTime createdAt, long serialNumber) =>
        $"BD-{createdAt.ToUniversalTime().Year}-{serialNumber:D7}";
}
