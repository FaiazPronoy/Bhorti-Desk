using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Security;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Auth;

// Reads the session cookie on every request, and blocks cross-site form posts.
public class SessionCookie(RequestDelegate next)
{
    public const string CookieName = "bhorti_session";
    // Browsers only let our own page set this header (CORS does not allow it from other
    // sites), so a request that carries it cannot be a forged form post.
    public const string CsrfHeader = "X-Bhorti-Request";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task InvokeAsync(HttpContext context, AuthService auth)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrEmpty(token))
        {
            var user = await auth.FindSessionAsync(Tokens.Sha256Hex(token));
            if (user is not null)
            {
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                    && !HttpMethods.IsOptions(context.Request.Method)
                    && !context.Request.Headers.ContainsKey(CsrfHeader))
                {
                    var error = AdmissionRuleException.CsrfRequired();
                    context.Response.StatusCode = error.StatusCode;
                    await context.Response.WriteAsJsonAsync(new Contracts.ApiErrorResponse(error.Code, error.Message));
                    return;
                }
                Access.Set(context, user);
            }
        }
        await next(context);
    }

    public static void Write(HttpResponse response, string token, AdmissionSettings settings)
    {
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = settings.SecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = Lifetime,
        });
    }

    public static void Clear(HttpResponse response, AdmissionSettings settings)
    {
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/", Secure = settings.SecureCookies, SameSite = SameSiteMode.Strict });
    }
}
