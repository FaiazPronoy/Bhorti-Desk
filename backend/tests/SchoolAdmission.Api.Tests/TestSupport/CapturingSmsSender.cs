using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Tests.TestSupport;

public partial class CapturingSmsSender : ISmsSender
{
    public ConcurrentQueue<(string Phone, string Message)> Sent { get; } = new();

    public Task SendAsync(string phone, string message)
    {
        Sent.Enqueue((phone, message));
        return Task.CompletedTask;
    }

    public string LastCodeFor(string phone) =>
        SixDigits().Match(Sent.Last(m => m.Phone == phone).Message).Value;

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}
