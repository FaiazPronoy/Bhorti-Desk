namespace SchoolAdmission.Api.Observability;

// The eight endpoints of the original assignment. They answer exactly as the contract
// says with default settings; every newer feature lives on other paths.
public static class FrozenContract
{
    private static readonly HashSet<string> Endpoints =
    [
        "POST classes",
        "GET classes",
        "GET classes/{classId}",
        "POST classes/{classId}/holds",
        "GET holds/{holdId}",
        "POST holds/{holdId}/confirm",
        "DELETE holds/{holdId}",
        "GET me/holds",
    ];

    public static bool Has(string? method, string? relativePath) =>
        Endpoints.Contains($"{method?.ToUpperInvariant()} {relativePath?.Trim('/')}");
}
