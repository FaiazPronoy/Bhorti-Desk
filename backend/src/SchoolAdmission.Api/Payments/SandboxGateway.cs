using System.Security.Cryptography;
using System.Text;
using SchoolAdmission.Api.Configuration;

namespace SchoolAdmission.Api.Payments;

// A pretend gateway that follows the shape of a real one: a hosted page, then a callback
// signed with a shared secret. No money moves, and every screen says so.
public class SandboxGateway(AdmissionSettings settings) : IPaymentGateway
{
    public string Name => "SANDBOX";

    public string HostedPageUrl(Guid paymentId) => $"/pay/{paymentId}";

    public string Sign(string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.PaymentSandboxSecret), Encoding.UTF8.GetBytes(body)));

    public bool IsGenuine(string body, string? signature)
    {
        if (string.IsNullOrEmpty(signature) || signature.Length != 64) return false;
        var expected = Encoding.ASCII.GetBytes(Sign(body));
        var given = Encoding.ASCII.GetBytes(signature.ToLowerInvariant());
        // Compares every byte, so the time taken does not tell how much of a guess was right.
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}
