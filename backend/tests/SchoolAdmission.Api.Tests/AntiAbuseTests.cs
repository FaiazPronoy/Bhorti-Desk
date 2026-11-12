using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

// Ways one person could grab more than their share, and the rules that stop them.
public class AntiAbuseTests
{
    private static HttpRequestMessage Post(string path, object? body = null, string? studentId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (studentId is not null) request.Headers.Add("X-Student-Id", studentId);
        request.Headers.Add("X-Bhorti-Request", "1");
        return request;
    }

    private static async Task<HttpClient> GuardianWithChildrenAsync(AdmissionApiFactory api, string phone, IEnumerable<string> children)
    {
        var client = api.CreateAnonymousClient();
        await client.SendAsync(Post("/auth/otp/request", new { phone }));
        var verify = await client.SendAsync(Post("/auth/otp/verify", new { phone, code = api.Sms.LastCodeFor(phone) }));
        client.DefaultRequestHeaders.Add("Cookie", verify.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        var index = 0;
        foreach (var child in children)
        {
            var added = await client.SendAsync(Post("/me/students", new
            {
                birthRegistrationNumber = child,
                fullName = $"Child {++index}",
                dateOfBirth = child[..4] + "-01-15",
                gender = "OTHER",
            }));
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }
        return client;
    }

    [Fact]
    public async Task One_family_cannot_hold_more_than_three_unpaid_seats_even_all_at_once()
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var classes = new List<string>();
        for (var i = 0; i < 6; i++) classes.Add(await ApiCalls.CreateClassAsync(staff, 10, $"Class {i}"));

        // Six children, six classes, all applied for in the same moment.
        var children = Enumerable.Range(1, 6).Select(i => "2016" + i.ToString("D13")).ToList();
        var family = await GuardianWithChildrenAsync(api, "01712345678", children);
        var responses = await Task.WhenAll(children.Select((child, i) =>
            family.SendAsync(Post($"/classes/{classes[i]}/holds", studentId: child))));

        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.Equal("TOO_MANY_HOLDS", await ApiCalls.ErrorCodeAsync(refused));
        }
    }

    [Fact]
    public async Task A_hold_that_ran_out_blocks_the_same_class_for_a_while()
    {
        await using var api = new AdmissionApiFactory(holdSeconds: 1, cooldownSeconds: 60);
        var client = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 5);
        var student = ApiCalls.NewStudentId();
        await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        var again = await ApiCalls.ApplyAsync(client, classId, student);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("APPLY_COOLDOWN", await ApiCalls.ErrorCodeAsync(again));
        Assert.True(again.Headers.RetryAfter?.Delta?.TotalSeconds > 0);

        // Another class is fine, and so is another student in this class.
        var other = await ApiCalls.CreateClassAsync(client, 5, "Class 7");
        Assert.Equal(HttpStatusCode.Created, (await ApiCalls.ApplyAsync(client, other, student)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId())).StatusCode);
    }

    [Fact]
    public async Task Cancelling_does_not_start_a_cooldown()
    {
        await using var api = new AdmissionApiFactory(cooldownSeconds: 60);
        var client = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 5);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        await ApiCalls.CancelAsync(client, holdId, student);

        Assert.Equal(HttpStatusCode.Created, (await ApiCalls.ApplyAsync(client, classId, student)).StatusCode);
    }

    [Fact]
    public async Task Sign_in_attempts_from_one_address_are_limited()
    {
        await using var api = new AdmissionApiFactory(rateLimits: true);
        var client = api.CreateAnonymousClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            var response = await client.SendAsync(Post("/auth/otp/request", new { phone = "0171" + i.ToString("D7") }));
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.Equal("RATE_LIMITED", await ApiCalls.ErrorCodeAsync(response));
            }
        }

        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.Accepted));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }
}
