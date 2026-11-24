using System.Text.RegularExpressions;
using SchoolAdmission.Api.Security;

namespace SchoolAdmission.Api.Services;

// Sends a text message. A real provider (for example an SSL Wireless or Twilio client)
// implements this; until one is set up, nothing leaves the server.
public interface ISmsSender
{
    Task SendAsync(string phone, string message);
}

// The demo provider: it only writes to the log. The number is masked, and any six digit
// code is hidden, so a sign-in code never lands in a log by this route.
public partial class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();

    public Task SendAsync(string phone, string message)
    {
        logger.LogInformation("SMS (demo, not sent) to {Phone}: {Message}", PhoneNumber.Masked(phone), SixDigits().Replace(message, "******"));
        return Task.CompletedTask;
    }
}
