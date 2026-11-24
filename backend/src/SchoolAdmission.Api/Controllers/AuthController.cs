using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Security;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService auth, AdmissionSettings settings) : ControllerBase
{
    // Always the same answer for a valid number, whether or not it has an account.
    [HttpPost("otp/request")]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest? request)
    {
        await auth.RequestOtpAsync(request?.Phone);
        return Accepted();
    }

    [HttpPost("otp/verify")]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<MeResponse> VerifyOtp([FromBody] VerifyOtpRequest? request)
    {
        var token = await auth.VerifyOtpAsync(request?.Phone, request?.Code);
        return await SignInAsync(token);
    }

    [HttpPost("staff/login")]
    [EnableRateLimiting(RateLimits.SignIn)]
    public async Task<MeResponse> StaffLogin([FromBody] StaffLoginRequest? request)
    {
        var token = await auth.StaffLoginAsync(request?.Email, request?.Password);
        return await SignInAsync(token);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(SessionCookie.CookieName, out var token) && !string.IsNullOrEmpty(token))
        {
            await auth.SignOutAsync(Tokens.Sha256Hex(token));
        }
        SessionCookie.Clear(Response, settings);
        return NoContent();
    }

    [HttpGet("me")]
    public MeResponse Me()
    {
        var user = Access.Find(HttpContext) ?? throw AdmissionRuleException.SignInRequired();
        return ToResponse(user);
    }

    private async Task<MeResponse> SignInAsync(string token)
    {
        SessionCookie.Write(Response, token, settings);
        var user = await auth.FindSessionAsync(Tokens.Sha256Hex(token)) ?? throw AdmissionRuleException.SignInRequired();
        return ToResponse(user);
    }

    private static MeResponse ToResponse(CurrentUser user) =>
        new(user.IsStaff ? "STAFF" : "GUARDIAN", user.Id, user.Role);
}
