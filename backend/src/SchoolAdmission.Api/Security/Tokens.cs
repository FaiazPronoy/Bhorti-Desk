using System.Security.Cryptography;
using System.Text;

namespace SchoolAdmission.Api.Security;

public static class Tokens
{
    // 256 random bits, safe to put in a cookie.
    public static string NewSessionToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // Six digits, from a cryptographic generator, never Random.
    public static string NewOtpCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
