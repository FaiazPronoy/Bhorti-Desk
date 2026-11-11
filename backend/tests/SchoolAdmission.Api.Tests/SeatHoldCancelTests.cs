using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class SeatHoldCancelTests
{
    [Fact]
    public async Task Cancel_returns_the_seat_and_repeat_cancel_is_fine()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 1);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        Assert.Equal(HttpStatusCode.NoContent, (await ApiCalls.CancelAsync(client, holdId, student)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ApiCalls.CancelAsync(client, holdId, student)).StatusCode);

        var hold = await ApiCalls.GetJsonAsync(client, $"/holds/{holdId}");
        Assert.Equal("CANCELLED", hold.GetProperty("status").GetString());

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("availableSeats").GetInt32());

        // The seat is really free: someone else gets it, and so can the same student.
        Assert.Equal(HttpStatusCode.Created, (await ApiCalls.ApplyAsync(client, classId, student)).StatusCode);
    }

    [Fact]
    public async Task Confirmed_hold_cannot_be_cancelled()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        Assert.Equal(HttpStatusCode.OK, (await ApiCalls.ConfirmAsync(client, holdId, student)).StatusCode);

        var cancel = await ApiCalls.CancelAsync(client, holdId, student);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("HOLD_ALREADY_CONFIRMED", await ApiCalls.ErrorCodeAsync(cancel));

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task Applying_again_while_holding_points_to_the_existing_hold()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        // One seat, so the class is full too. The student should still hear about their own hold.
        var classId = await ApiCalls.CreateClassAsync(client, 1);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        var again = await ApiCalls.ApplyAsync(client, classId, student);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var body = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ALREADY_APPLIED", body.GetProperty("code").GetString());
        Assert.Equal(holdId, body.GetProperty("holdId").GetString());
    }
}
