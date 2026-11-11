using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class InputValidationTests
{
    [Fact]
    public async Task Bad_input_never_returns_500()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        var badBodies = new[]
        {
            "", "null", "{", "[]", "123", "\"text\"", "{}",
            "{\"name\":\"\",\"totalSeats\":10}",
            "{\"name\":\"   \",\"totalSeats\":10}",
            "{\"name\":\"Class 6\"}",
            "{\"name\":\"Class 6\",\"totalSeats\":0}",
            "{\"name\":\"Class 6\",\"totalSeats\":-5}",
            "{\"name\":\"Class 6\",\"totalSeats\":1.5}",
            "{\"name\":\"Class 6\",\"totalSeats\":\"ten\"}",
            "{\"name\":\"Class 6\",\"totalSeats\":99999999999999}",
            "{\"name\":\"Class 6\",\"totalSeats\":100001}",
            "{\"name\":123,\"totalSeats\":10}",
            "{\"name\":null,\"totalSeats\":null}",
            "{\"name\":\"" + new string('x', 101) + "\",\"totalSeats\":10}",
            "{\"name\":\"Class\\u0000Six\",\"totalSeats\":10}",
        };

        var responses = new List<(string What, HttpResponseMessage Response)>();
        foreach (var body in badBodies)
        {
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            responses.Add(($"POST /classes {body}", await client.PostAsync("/classes", content)));
        }

        responses.Add(("POST /classes as plain text",
            await client.PostAsync("/classes", new StringContent("name=Class 6", Encoding.UTF8, "text/plain"))));
        responses.Add(("POST /classes, body too large",
            await client.PostAsync("/classes", new StringContent(
                "{\"name\":\"" + new string('x', 40_000) + "\",\"totalSeats\":10}", Encoding.UTF8, "application/json"))));

        var badStudentIds = new[] { "", "   ", "abc", "1234", "1234567890123456", "123456789012345678", "2015-0123-4567-890", "' or 1=1 --" };
        foreach (var badId in badStudentIds)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
            request.Headers.TryAddWithoutValidation("X-Student-Id", badId);
            responses.Add(($"apply with X-Student-Id '{badId}'", await client.SendAsync(request)));
        }
        responses.Add(("apply with no header", await client.PostAsync($"/classes/{classId}/holds", null)));
        responses.Add(("my holds with no header", await client.GetAsync("/me/holds")));
        responses.Add(("confirm with no header", await client.PostAsync($"/holds/{holdId}/confirm", null)));
        responses.Add(("cancel with no header", await client.DeleteAsync($"/holds/{holdId}")));

        foreach (var badId in new[] { "not-a-guid", "123", "00000000-0000-0000-0000-000000000000", Guid.NewGuid().ToString(), "%27%20or%201%3D1" })
        {
            responses.Add(($"GET /classes/{badId}", await client.GetAsync($"/classes/{badId}")));
            responses.Add(($"GET /holds/{badId}", await client.GetAsync($"/holds/{badId}")));
            responses.Add(($"apply to /classes/{badId}", await ApiCalls.ApplyAsync(client, badId, student)));
            responses.Add(($"confirm /holds/{badId}", await ApiCalls.ConfirmAsync(client, badId, student)));
            responses.Add(($"cancel /holds/{badId}", await ApiCalls.CancelAsync(client, badId, student)));
        }

        responses.Add(("unknown path", await client.GetAsync("/nothing-here")));
        responses.Add(("wrong method on /classes", await client.DeleteAsync("/classes")));
        responses.Add(("wrong method on a hold", await client.PutAsync($"/holds/{holdId}", null)));

        foreach (var (what, response) in responses)
        {
            Assert.True((int)response.StatusCode is >= 400 and < 500,
                $"{what} answered {(int)response.StatusCode}, expected a 4xx");
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.TryGetProperty("code", out var code) && code.GetString()!.Length > 0, $"{what} has no error code");
            Assert.True(body.TryGetProperty("message", out _), $"{what} has no message");
        }
    }

    [Fact]
    public async Task Each_bad_field_is_named_in_fieldErrors()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/classes", new { name = "", totalSeats = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_REQUEST", body.GetProperty("code").GetString());
        var fields = body.GetProperty("fieldErrors");
        Assert.True(fields.TryGetProperty("name", out _));
        Assert.True(fields.TryGetProperty("totalSeats", out _));
    }

    [Theory]
    [InlineData("", "STUDENT_ID_REQUIRED")]
    [InlineData("12345", "STUDENT_ID_INVALID")]
    [InlineData("2015 0123 4567 8901", "STUDENT_ID_INVALID")]
    public async Task Student_id_errors_use_their_own_codes(string header, string expectedCode)
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.TryAddWithoutValidation("X-Student-Id", header);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await ApiCalls.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Spaces_around_the_student_id_are_ignored()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);

        var response = await ApiCalls.ApplyAsync(client, classId, "  " + ApiCalls.NewStudentId() + " ");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
