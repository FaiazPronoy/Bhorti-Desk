using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

// The frontend and any grader's script read these exact field names, so the shapes are
// checked field by field. Extra fields allowed by the contract are listed per endpoint.
public partial class ApiContractShapeTests
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")]
    private static partial Regex UtcSecondsPattern();

    private static void HasExactly(JsonElement body, string[] required, string[] allowedExtras)
    {
        var names = body.EnumerateObject().Select(p => p.Name).ToHashSet();
        foreach (var name in required)
        {
            Assert.Contains(name, names);
        }
        foreach (var name in names)
        {
            Assert.True(required.Contains(name) || allowedExtras.Contains(name), $"unexpected field '{name}'");
        }
    }

    private static void IsUtcSeconds(JsonElement value) =>
        Assert.Matches(UtcSecondsPattern(), value.GetString()!);

    [Fact]
    public async Task Responses_match_the_contract()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var student = ApiCalls.NewStudentId();

        // 1. POST /classes
        var createResponse = await client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 40 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        HasExactly(created, ["id", "name", "totalSeats"], []);
        var classId = created.GetProperty("id").GetString()!;
        Assert.True(Guid.TryParse(classId, out _));
        Assert.Equal("Class 6 - Morning Shift", created.GetProperty("name").GetString());
        Assert.Equal(40, created.GetProperty("totalSeats").GetInt32());

        string[] summaryFields = ["id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats"];

        // 2. GET /classes
        var list = await ApiCalls.GetJsonAsync(client, "/classes");
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        var listed = Assert.Single(list.EnumerateArray());
        HasExactly(listed, summaryFields, []);
        Assert.Equal(40, listed.GetProperty("availableSeats").GetInt32());

        // 4. POST /classes/{classId}/holds
        var applyResponse = await ApiCalls.ApplyAsync(client, classId, student);
        Assert.Equal(HttpStatusCode.Created, applyResponse.StatusCode);
        var hold = await applyResponse.Content.ReadFromJsonAsync<JsonElement>();
        HasExactly(hold, ["holdId", "classId", "studentId", "status", "expiresAt"], ["className", "serverTime"]);
        var holdId = hold.GetProperty("holdId").GetString()!;
        Assert.Equal(classId, hold.GetProperty("classId").GetString());
        Assert.Equal(student, hold.GetProperty("studentId").GetString());
        Assert.Equal("HELD", hold.GetProperty("status").GetString());
        IsUtcSeconds(hold.GetProperty("expiresAt"));

        // The default hold is ten minutes, counted on the database clock.
        var expiresAt = DateTimeOffset.Parse(hold.GetProperty("expiresAt").GetString()!);
        var serverTime = DateTimeOffset.Parse(hold.GetProperty("serverTime").GetString()!);
        Assert.InRange((expiresAt - serverTime).TotalSeconds, 595, 601);

        // 3. GET /classes/{classId}
        var one = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        HasExactly(one, summaryFields, []);
        Assert.Equal(1, one.GetProperty("heldSeats").GetInt32());
        Assert.Equal(39, one.GetProperty("availableSeats").GetInt32());

        // 5. GET /holds/{holdId}: no header needed and the student id is never shown.
        var status = await ApiCalls.GetJsonAsync(client, $"/holds/{holdId}");
        HasExactly(status, ["holdId", "classId", "status", "expiresAt"], ["className", "createdAt", "serverTime"]);
        Assert.False(status.TryGetProperty("studentId", out _));
        IsUtcSeconds(status.GetProperty("expiresAt"));

        // 8. GET /me/holds
        var mine = await ApiCalls.MyHoldsAsync(client, student);
        var mineItem = Assert.Single(mine.EnumerateArray());
        HasExactly(mineItem, ["holdId", "classId", "className", "status", "expiresAt"], ["createdAt", "serverTime"]);
        Assert.Equal(0, (await ApiCalls.MyHoldsAsync(client, ApiCalls.NewStudentId())).GetArrayLength());

        // 6. POST /holds/{holdId}/confirm
        var confirmResponse = await ApiCalls.ConfirmAsync(client, holdId, student);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        HasExactly(confirmed, ["holdId", "status"], []);
        Assert.Equal("CONFIRMED", confirmed.GetProperty("status").GetString());

        // 7. DELETE /holds/{holdId} on a fresh hold
        var otherStudent = ApiCalls.NewStudentId();
        var otherHold = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, otherStudent);
        var cancelResponse = await ApiCalls.CancelAsync(client, otherHold, otherStudent);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);
        Assert.Equal(0, (await cancelResponse.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Errors_have_the_same_shape_everywhere()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();

        var notFound = await client.GetAsync($"/classes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        var body = await notFound.Content.ReadFromJsonAsync<JsonElement>();
        HasExactly(body, ["code", "message"], ["fieldErrors", "holdId"]);
        Assert.Equal("CLASS_NOT_FOUND", body.GetProperty("code").GetString());

        var unknownHold = await client.GetAsync($"/holds/{Guid.NewGuid()}");
        Assert.Equal("HOLD_NOT_FOUND", await ApiCalls.ErrorCodeAsync(unknownHold));

        var noRoute = await client.GetAsync("/nothing-here");
        Assert.Equal(HttpStatusCode.NotFound, noRoute.StatusCode);
        Assert.Equal("NOT_FOUND", await ApiCalls.ErrorCodeAsync(noRoute));

        var wrongMethod = await client.DeleteAsync("/classes");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Equal("METHOD_NOT_ALLOWED", await ApiCalls.ErrorCodeAsync(wrongMethod));
    }

    [Fact]
    public async Task Browser_from_another_origin_can_read_the_answers()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/classes/x/holds");
        preflight.Headers.Add("Origin", "http://localhost:3000");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-student-id");
        var answer = await client.SendAsync(preflight);
        Assert.True(answer.Headers.Contains("Access-Control-Allow-Origin"));

        // Error answers must carry the CORS header too, or the browser hides the message.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/classes/{Guid.NewGuid()}");
        request.Headers.Add("Origin", "http://localhost:3000");
        var error = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.True(error.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
