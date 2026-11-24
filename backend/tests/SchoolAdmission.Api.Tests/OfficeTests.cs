using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class OfficeTests
{
    [Fact]
    public async Task Seats_can_shrink_but_never_below_the_seats_in_use()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 5);
        for (var i = 0; i < 3; i++) await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, ApiCalls.NewStudentId());

        var tooFew = await staff.PutAsJsonAsync($"/office/classes/{classId}", new { name = "Class 6", totalSeats = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, tooFew.StatusCode);
        Assert.Equal("office.edit.belowInUse", (await tooFew.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fieldErrors").GetProperty("totalSeats").GetString());

        Assert.Equal(HttpStatusCode.OK, (await staff.PutAsJsonAsync($"/office/classes/{classId}", new { name = "Class 6", totalSeats = 3 })).StatusCode);
        Assert.Equal(0, (await ApiCalls.GetJsonAsync(client, $"/classes/{classId}")).GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Shrinking_while_people_apply_never_leaves_more_seats_used_than_exist()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 40);

        var applies = Enumerable.Range(0, 120).Select(_ => ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId()));
        var shrinks = Enumerable.Range(0, 10).Select(i => staff.PutAsJsonAsync($"/office/classes/{classId}", new { name = "Class 6", totalSeats = 40 - i * 3 }));
        await Task.WhenAll(applies.Cast<Task>().Concat(shrinks));

        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select count(*) from admission_classes c
             where (select count(*) from seat_holds h where h.admission_class_id = c.id
                      and (h.status = 'CONFIRMED' or (h.status = 'HELD' and h.expires_at > clock_timestamp()))) > c.total_seats
            """, connection);
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task A_closed_class_takes_no_new_applications_and_the_action_is_audited()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 5);

        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/office/classes/{classId}/close", null)).StatusCode);
        Assert.Equal("SESSION_NOT_OPEN", await ApiCalls.ErrorCodeAsync(await ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId())));

        var audit = await ApiCalls.GetJsonAsync(staff, "/office/audit?action=CLASS_CLOSED");
        Assert.Single(audit.EnumerateArray());
    }

    [Fact]
    public async Task Reports_count_the_funnel_and_the_csv_is_safe_to_open()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 5, "=HYPERLINK(\"http://x\")");
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        await ApiCalls.ConfirmAsync(client, holdId, student);
        await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, ApiCalls.NewStudentId());

        var report = await ApiCalls.GetJsonAsync(staff, "/office/reports");
        var funnel = report.GetProperty("funnel");
        Assert.Equal(2, funnel.GetProperty("applied").GetInt32());
        Assert.Equal(1, funnel.GetProperty("onHoldNow").GetInt32());
        Assert.Equal(1, funnel.GetProperty("paid").GetInt32());

        var csv = await staff.GetStringAsync("/office/reports/classes.csv");
        Assert.Contains("\"'=HYPERLINK(\"\"http://x\"\")\",5,2,40,0,no", csv);
    }

    [Fact]
    public async Task Office_endpoints_need_staff()
    {
        await using var api = new AdmissionApiFactory();
        var anonymous = api.CreateAnonymousClient();
        foreach (var path in new[] { "/office/classes", "/office/reports", "/office/reports/classes.csv", "/office/audit", "/office/audit.csv" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        }
    }
}
