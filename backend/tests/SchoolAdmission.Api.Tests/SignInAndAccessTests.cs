using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

// Who may do what. Every rule here is a way someone could try to cheat.
public class SignInAndAccessTests
{
    private static HttpRequestMessage Post(string path, object? body = null, string? studentId = null, bool csrf = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (studentId is not null) request.Headers.Add("X-Student-Id", studentId);
        if (csrf) request.Headers.Add("X-Bhorti-Request", "1");
        return request;
    }

    // Signs in through the real OTP flow and returns a client that carries the cookie.
    private static async Task<HttpClient> SignInByOtpAsync(AdmissionApiFactory api, string phone)
    {
        var client = api.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(Post("/auth/otp/request", new { phone }))).StatusCode);
        var code = api.Sms.LastCodeFor(phone);
        var verify = await client.SendAsync(Post("/auth/otp/verify", new { phone, code }));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var cookie = verify.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("bhorti_session="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        return client;
    }

    private static object Student(string brn) =>
        new { birthRegistrationNumber = brn, fullName = "Ayesha Rahman", dateOfBirth = brn[..4] + "-03-14", gender = "FEMALE" };

    [Fact]
    public async Task Applying_needs_a_signed_in_guardian()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(staff, 5);

        var anonymous = await api.CreateAnonymousClient().SendAsync(Post($"/classes/{classId}/holds", studentId: "20150123456789012"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("SIGN_IN_REQUIRED", await ApiCalls.ErrorCodeAsync(anonymous));
    }

    [Fact]
    public async Task Otp_sign_in_then_apply_for_your_own_child()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var classId = await ApiCalls.CreateClassAsync(api.CreateSignedInClient(), 5);
        var guardian = await SignInByOtpAsync(api, "01712345678");

        var me = await guardian.GetFromJsonAsync<JsonElement>("/auth/me");
        Assert.Equal("GUARDIAN", me.GetProperty("kind").GetString());

        Assert.Equal(HttpStatusCode.Created, (await guardian.SendAsync(Post("/me/students", Student("20160123456789012")))).StatusCode);
        var apply = await guardian.SendAsync(Post($"/classes/{classId}/holds", studentId: "20160123456789012"));
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
    }

    [Fact]
    public async Task Nobody_can_apply_in_another_familys_childs_name()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var classId = await ApiCalls.CreateClassAsync(api.CreateSignedInClient(), 5);
        var parent = await SignInByOtpAsync(api, "01712345678");
        var stranger = await SignInByOtpAsync(api, "01812345678");
        const string child = "20160123456789012";
        await parent.SendAsync(Post("/me/students", Student(child)));

        // The stranger cannot add the same child to their account...
        var take = await stranger.SendAsync(Post("/me/students", Student(child)));
        Assert.Equal(HttpStatusCode.Conflict, take.StatusCode);
        Assert.Equal("STUDENT_TAKEN", await ApiCalls.ErrorCodeAsync(take));

        // ...and cannot apply with the number without adding it.
        var apply = await stranger.SendAsync(Post($"/classes/{classId}/holds", studentId: child));
        Assert.Equal(HttpStatusCode.Forbidden, apply.StatusCode);
        Assert.Equal("STUDENT_NOT_YOURS", await ApiCalls.ErrorCodeAsync(apply));
    }

    [Fact]
    public async Task Birth_year_must_match_the_number()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var guardian = await SignInByOtpAsync(api, "01712345678");
        var wrong = new { birthRegistrationNumber = "20160123456789012", fullName = "Ayesha Rahman", dateOfBirth = "2014-03-14", gender = "FEMALE" };

        var response = await guardian.SendAsync(Post("/me/students", wrong));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("fieldErrors").TryGetProperty("birthRegistrationNumber", out _));
    }

    [Fact]
    public async Task Only_the_school_office_can_create_a_class()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var guardian = await SignInByOtpAsync(api, "01712345678");

        var response = await guardian.SendAsync(Post("/classes", new { name = "Class 6", totalSeats = 10 }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("NOT_ALLOWED", await ApiCalls.ErrorCodeAsync(response));

        var anonymous = await api.CreateAnonymousClient().SendAsync(Post("/classes", new { name = "Class 6", totalSeats = 10 }));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task A_request_without_the_app_header_is_refused()
    {
        // A forged form on another site can send the cookie but not a custom header.
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var guardian = await SignInByOtpAsync(api, "01712345678");

        var response = await guardian.SendAsync(Post("/me/students", Student("20160123456789012"), csrf: false));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("CSRF_REQUIRED", await ApiCalls.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_wrong_code_five_times_burns_it_and_a_code_works_once()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var client = api.CreateAnonymousClient();
        const string phone = "01712345678";
        await client.SendAsync(Post("/auth/otp/request", new { phone }));
        var code = api.Sms.LastCodeFor(phone);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var attempt = await client.SendAsync(Post("/auth/otp/verify", new { phone, code = wrong }));
            Assert.Equal("OTP_WRONG", await ApiCalls.ErrorCodeAsync(attempt));
        }
        // Even the right code is refused now.
        var late = await client.SendAsync(Post("/auth/otp/verify", new { phone, code }));
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
    }

    [Fact]
    public async Task Codes_cannot_be_asked_for_again_and_again()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var client = api.CreateAnonymousClient();
        await client.SendAsync(Post("/auth/otp/request", new { phone = "01712345678" }));

        var again = await client.SendAsync(Post("/auth/otp/request", new { phone = "+880 1712-345678" }));
        Assert.Equal(HttpStatusCode.TooManyRequests, again.StatusCode);
        Assert.Equal("OTP_TOO_SOON", await ApiCalls.ErrorCodeAsync(again));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("01212345678")]
    [InlineData("017123456789")]
    public async Task Bad_phone_numbers_are_refused(string phone)
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var response = await api.CreateAnonymousClient().SendAsync(Post("/auth/otp/request", new { phone }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("PHONE_INVALID", await ApiCalls.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Staff_sign_in_rejects_a_wrong_password_the_same_way_as_an_unknown_email()
    {
        await using var api = new AdmissionApiFactory(adminEmail: "admin@school.test", adminPassword: "correct horse battery");
        var client = api.CreateAnonymousClient();

        var wrongPassword = await client.SendAsync(Post("/auth/staff/login", new { email = "admin@school.test", password = "wrong password!" }));
        var unknownEmail = await client.SendAsync(Post("/auth/staff/login", new { email = "nobody@school.test", password = "wrong password!" }));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownEmail.Content.ReadAsStringAsync());

        var ok = await client.SendAsync(Post("/auth/staff/login", new { email = "ADMIN@school.test", password = "correct horse battery" }));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        await using var api = new AdmissionApiFactory(signInRequired: true);
        var guardian = await SignInByOtpAsync(api, "01712345678");
        Assert.Equal(HttpStatusCode.NoContent, (await guardian.SendAsync(Post("/auth/logout"))).StatusCode);

        var me = await guardian.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
