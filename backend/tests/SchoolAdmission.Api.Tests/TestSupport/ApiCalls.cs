using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Api.Tests.TestSupport;

// Small helpers so each test reads like the steps a person would take.
public static class ApiCalls
{
    private static int studentCounter;

    // A different valid 17 digit birth registration number on every call.
    public static string NewStudentId()
    {
        var next = Interlocked.Increment(ref studentCounter);
        return "2015" + Random.Shared.Next(100_000, 999_999) + next.ToString("D7");
    }

    public static async Task<string> CreateClassAsync(HttpClient client, int totalSeats, string name = "Class 6 - Morning Shift")
    {
        var response = await client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    public static Task<HttpResponseMessage> ApplyAsync(HttpClient client, string classId, string studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.Add("X-Student-Id", studentId);
        return client.SendAsync(request);
    }

    public static async Task<string> ApplyAndGetHoldIdAsync(HttpClient client, string classId, string studentId)
    {
        var response = await ApplyAsync(client, classId, studentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("holdId").GetString()!;
    }

    public static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string holdId, string studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        request.Headers.Add("X-Student-Id", studentId);
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> CancelAsync(HttpClient client, string holdId, string studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        request.Headers.Add("X-Student-Id", studentId);
        return client.SendAsync(request);
    }

    public static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> MyHoldsAsync(HttpClient client, string studentId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        request.Headers.Add("X-Student-Id", studentId);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("code").GetString()!;
    }
}
