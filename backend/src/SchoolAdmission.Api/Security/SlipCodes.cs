using System.Security.Cryptography;
using System.Text;
using SchoolAdmission.Api.Configuration;

namespace SchoolAdmission.Api.Security;

// The code in the slip's QR: the hold id plus the first 16 bytes of an HMAC of it, in
// URL-safe base64. Anyone can read it, nobody without the secret can make a new one,
// and changing one character makes the check fail.
public class SlipCodes(AdmissionSettings settings)
{
    private const int MacLength = 16;

    public string Create(Guid holdId)
    {
        var id = holdId.ToByteArray();
        return ToBase64Url([.. id, .. Mac(id)]);
    }

    public Guid? Read(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64) return null;
        byte[] bytes;
        try
        {
            bytes = FromBase64Url(code);
        }
        catch (FormatException)
        {
            return null;
        }
        if (bytes.Length != 16 + MacLength) return null;
        var id = bytes[..16];
        return CryptographicOperations.FixedTimeEquals(bytes[16..], Mac(id)) ? new Guid(id) : null;
    }

    private byte[] Mac(byte[] id) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.SlipSecret), id)[..MacLength];

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }
}
