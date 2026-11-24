using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class AdmissionSessionTests
{
    private static async Task<string> CreateSessionAsync(HttpClient staff, DateTimeOffset opensAt, DateTimeOffset closesAt, bool publish = true)
    {
        var created = await staff.PostAsJsonAsync("/office/sessions", new { academicYear = 2027, name = "Admission 2027", opensAt, closesAt });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        if (publish) Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/office/sessions/{id}/publish", null)).StatusCode);
        return id;
    }

    private static async Task AttachAsync(HttpClient staff, string sessionId, string classId) =>
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/office/sessions/{sessionId}/classes", new { classId })).StatusCode);

    [Fact]
    public async Task A_class_with_no_session_stays_open_even_when_a_session_is_closed()
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var now = DateTimeOffset.UtcNow;
        await CreateSessionAsync(staff, now.AddDays(-3), now.AddDays(-1));
        var classId = await ApiCalls.CreateClassAsync(staff, 3);

        var apply = await ApiCalls.ApplyAsync(api.CreateAnonymousClient(), classId, ApiCalls.NewStudentId());

        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(-3, -1)]
    public async Task Applying_outside_the_window_is_refused_and_takes_no_seat(int opensInDays, int closesInDays)
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var now = DateTimeOffset.UtcNow;
        var sessionId = await CreateSessionAsync(staff, now.AddDays(opensInDays), now.AddDays(closesInDays));
        var classId = await ApiCalls.CreateClassAsync(staff, 3);
        await AttachAsync(staff, sessionId, classId);

        var apply = await ApiCalls.ApplyAsync(api.CreateAnonymousClient(), classId, ApiCalls.NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, apply.StatusCode);
        Assert.Equal("SESSION_NOT_OPEN", await ApiCalls.ErrorCodeAsync(apply));
        var seats = await ApiCalls.GetJsonAsync(staff, $"/classes/{classId}");
        Assert.Equal(3, seats.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Applying_inside_the_window_works_and_the_original_response_is_unchanged()
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var now = DateTimeOffset.UtcNow;
        var sessionId = await CreateSessionAsync(staff, now.AddHours(-1), now.AddHours(1));
        var classId = await ApiCalls.CreateClassAsync(staff, 3);
        await AttachAsync(staff, sessionId, classId);

        var apply = await ApiCalls.ApplyAsync(api.CreateAnonymousClient(), classId, ApiCalls.NewStudentId());
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        var seats = await ApiCalls.GetJsonAsync(staff, $"/classes/{classId}");
        Assert.Equal(["availableSeats", "confirmedSeats", "heldSeats", "id", "name", "totalSeats"],
            seats.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Current_season_uses_the_server_clock_for_its_window()
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var anonymous = api.CreateAnonymousClient();
        Assert.Equal(JsonValueKind.Null, (await ApiCalls.GetJsonAsync(anonymous, "/season/current")).GetProperty("session").ValueKind);

        var now = DateTimeOffset.UtcNow;
        await CreateSessionAsync(staff, now.AddHours(2), now.AddDays(2));

        var season = await ApiCalls.GetJsonAsync(anonymous, "/season/current");
        Assert.Equal("UPCOMING", season.GetProperty("session").GetProperty("window").GetString());
        Assert.True(season.TryGetProperty("serverTime", out _));
    }

    [Fact]
    public async Task Only_one_session_can_be_published_at_a_time()
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var now = DateTimeOffset.UtcNow;
        await CreateSessionAsync(staff, now.AddDays(1), now.AddDays(2));
        var second = await CreateSessionAsync(staff, now.AddDays(3), now.AddDays(4), publish: false);

        var response = await staff.PostAsync($"/office/sessions/{second}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SESSION_ALREADY_PUBLISHED", await ApiCalls.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Office_session_endpoints_need_staff_and_each_change_is_audited()
    {
        await using var api = new AdmissionApiFactory();
        var anonymous = await api.CreateAnonymousClient().GetAsync("/office/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var staff = api.CreateSignedInClient();
        var now = DateTimeOffset.UtcNow;
        await CreateSessionAsync(staff, now.AddDays(1), now.AddDays(2));

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from audit_log where action in ('SESSION_CREATED', 'SESSION_PUBLISHED')", connection);
        Assert.Equal(2L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task A_bad_session_form_names_each_field()
    {
        await using var api = new AdmissionApiFactory();
        var staff = api.CreateSignedInClient();
        var now = DateTimeOffset.UtcNow;

        var response = await staff.PostAsJsonAsync("/office/sessions", new { academicYear = 1990, name = "", opensAt = now, closesAt = now.AddDays(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fieldErrors");
        foreach (var name in new[] { "academicYear", "name", "closesAt" }) Assert.True(fields.TryGetProperty(name, out _), name);
    }
}
