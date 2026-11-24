using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Security;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class SlipTests
{
    private static async Task<JsonElement> SlipAsync(HttpClient client, string holdId, string student)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/applications/{holdId}/slip");
        request.Headers.Add("X-Student-Id", student);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task A_paid_slip_has_a_qr_and_its_code_verifies_with_only_safe_facts()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        await ApiCalls.ConfirmAsync(client, holdId, student);

        var slip = await SlipAsync(client, holdId, student);
        Assert.True(slip.GetProperty("qr").GetArrayLength() >= 21);
        Assert.DoesNotContain(student, slip.GetRawText());

        var verify = await ApiCalls.GetJsonAsync(api.CreateAnonymousClient(), $"/verify/{slip.GetProperty("verifyCode").GetString()}");
        Assert.Equal("PAID", verify.GetProperty("admissionStatus").GetString());
        Assert.EndsWith(student[^4..], verify.GetProperty("maskedStudentId").GetString());
        Assert.Equal(["admissionStatus", "className", "maskedStudentId"], verify.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_tampered_code_fails()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        await ApiCalls.ConfirmAsync(client, holdId, student);
        var code = (await SlipAsync(client, holdId, student)).GetProperty("verifyCode").GetString()!;

        var flipped = code[..5] + (code[5] == 'A' ? 'B' : 'A') + code[6..];
        var response = await client.GetAsync($"/verify/{flipped}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("SLIP_INVALID", await ApiCalls.ErrorCodeAsync(response));
    }

    [Fact]
    public void A_code_made_with_another_secret_or_for_another_id_fails()
    {
        var real = new SlipCodes(new AdmissionSettings { ConnectionString = "x", HoldDurationSeconds = 600, CorsAllowedOrigins = [], SlipSecret = "real secret" });
        var forger = new SlipCodes(new AdmissionSettings { ConnectionString = "x", HoldDurationSeconds = 600, CorsAllowedOrigins = [], SlipSecret = "guessed" });
        var id = Guid.NewGuid();

        Assert.Equal(id, real.Read(real.Create(id)));
        Assert.Null(real.Read(forger.Create(id)));
        Assert.Null(real.Read("not-base64!!"));
        Assert.Null(real.Read(""));
    }

    [Fact]
    public async Task An_unpaid_hold_has_no_slip()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/applications/{holdId}/slip");
        request.Headers.Add("X-Student-Id", student);
        var response = await client.SendAsync(request);

        Assert.Equal("NOT_PAID_YET", await ApiCalls.ErrorCodeAsync(response));
    }
}
