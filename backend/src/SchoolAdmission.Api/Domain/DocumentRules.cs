namespace SchoolAdmission.Api.Domain;

// What an uploaded file really is, read from its first bytes. The name and the type the
// browser claims are never trusted: a renamed .exe still starts with "MZ".
public static class DocumentRules
{
    public const string Photo = "PHOTO";
    public const string Certificate = "CERTIFICATE";

    public const int MaxPhotoBytes = 300 * 1024;
    public const int MaxCertificateBytes = 1024 * 1024;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();

    public static string? ParseKind(string kind) => kind.ToUpperInvariant() switch
    {
        "PHOTO" => Photo,
        "CERTIFICATE" => Certificate,
        _ => null,
    };

    public static int MaxBytes(string kind) => kind == Photo ? MaxPhotoBytes : MaxCertificateBytes;

    // The real content type, or null if the file is not one this kind accepts.
    public static string? DetectContentType(string kind, ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(Jpeg)) return "image/jpeg";
        if (data.StartsWith(Png)) return "image/png";
        if (kind == Certificate && data.StartsWith(Pdf)) return "application/pdf";
        return null;
    }
}
