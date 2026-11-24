using System.Text.RegularExpressions;

namespace SchoolAdmission.Api.Observability;

// Gives every request an id, sends it back in X-Request-Id, and puts it on every log line
// written while the request runs. A caller's own id is kept if it is short and plain.
public partial class RequestIds(RequestDelegate next, ILogger<RequestIds> logger)
{
    public const string Header = "X-Request-Id";

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex Plain();

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = Plain().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;
        using (logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = id }))
        {
            await next(context);
        }
    }
}
