using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Security;

// How fast one person or one address may call the API. The seat rule never depends on
// these; they only stop bots and scripts from flooding sign-in and applications.
public static class RateLimits
{
    public const string SignIn = "sign-in";
    public const string Apply = "apply";

    public static void Add(IServiceCollection services, AdmissionSettings settings)
    {
        // nginx in front adds X-Forwarded-For. Only proxies on a private network are
        // believed, so a caller on the internet cannot pick their own address.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8" })
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }
                await context.HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse(ApiErrorCode.RateLimited,
                    "Too many tries in a short time. Wait a little and try again."), token);
            };

            if (!settings.RateLimitsEnabled)
            {
                options.AddPolicy(SignIn, _ => RateLimitPartition.GetNoLimiter("off"));
                options.AddPolicy(Apply, _ => RateLimitPartition.GetNoLimiter("off"));
                return;
            }

            // Every request: 300 a minute per person or address.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(Key(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                }));

            // Sign-in: 10 tries per 10 minutes per address, on top of the per-number OTP limits.
            options.AddPolicy(SignIn, context =>
                RateLimitPartition.GetFixedWindowLimiter("ip:" + Address(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(10),
                }));

            // Apply, pay, cancel: a burst of 10, then one every 3 seconds per person.
            options.AddPolicy(Apply, context =>
                RateLimitPartition.GetTokenBucketLimiter(Key(context), _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(3),
                    AutoReplenishment = true,
                }));
        });
    }

    // A signed-in person is limited as themselves; anyone else by their address.
    private static string Key(HttpContext context) =>
        Access.Find(context) is { } user ? "user:" + user.Id : "ip:" + Address(context);

    private static string Address(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
}
