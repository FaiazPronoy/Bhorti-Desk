using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

// With no settings changed, nobody has to sign in and nothing is rate limited. This is the
// promise the original eight endpoints were built on.
public class DefaultSettingsContractTests
{
    [Fact]
    public async Task All_eight_endpoints_work_with_no_login_on_default_settings()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var student = ApiCalls.NewStudentId();

        var created = await client.PostAsJsonAsync("/classes", new { name = "Class 6", totalSeats = 2 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var classId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/classes/{classId}")).StatusCode);

        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/holds/{holdId}")).StatusCode);
        Assert.Equal(1, (await ApiCalls.MyHoldsAsync(client, student)).GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await ApiCalls.ConfirmAsync(client, holdId, student)).StatusCode);

        var otherStudent = ApiCalls.NewStudentId();
        var otherHold = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, otherStudent);
        Assert.Equal(HttpStatusCode.NoContent, (await ApiCalls.CancelAsync(client, otherHold, otherStudent)).StatusCode);
    }

    [Fact]
    public async Task A_burst_from_one_address_is_not_rate_limited_on_default_settings()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 500);

        var responses = await Task.WhenAll(Enumerable.Range(0, 400)
            .Select(_ => ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId())));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
    }

    [Fact]
    public async Task A_student_can_apply_again_at_once_after_an_expiry_on_default_settings()
    {
        await using var api = new AdmissionApiFactory(holdSeconds: 1);
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 1);
        var student = ApiCalls.NewStudentId();
        await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        await Task.Delay(TimeSpan.FromSeconds(2.2));

        Assert.Equal(HttpStatusCode.Created, (await ApiCalls.ApplyAsync(client, classId, student)).StatusCode);
    }
}
