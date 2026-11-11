using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class SeatHoldConfirmTests
{
    [Fact]
    public async Task Confirm_twice_counts_one_confirmed_seat()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        // Two clicks on the pay button at the same moment, then one more later.
        var together = await Task.WhenAll(
            ApiCalls.ConfirmAsync(client, holdId, student),
            ApiCalls.ConfirmAsync(client, holdId, student));
        var later = await ApiCalls.ConfirmAsync(client, holdId, student);

        foreach (var response in together.Append(later))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(holdId, body.GetProperty("holdId").GetString());
            Assert.Equal("CONFIRMED", body.GetProperty("status").GetString());
        }

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, seats.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, seats.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Another_student_cannot_confirm_or_cancel_my_hold()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var owner = ApiCalls.NewStudentId();
        var stranger = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, owner);

        var confirm = await ApiCalls.ConfirmAsync(client, holdId, stranger);
        Assert.Equal(HttpStatusCode.Forbidden, confirm.StatusCode);
        Assert.Equal("HOLD_NOT_OWNED", await ApiCalls.ErrorCodeAsync(confirm));

        var cancel = await ApiCalls.CancelAsync(client, holdId, stranger);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal("HOLD_NOT_OWNED", await ApiCalls.ErrorCodeAsync(cancel));

        var hold = await ApiCalls.GetJsonAsync(client, $"/holds/{holdId}");
        Assert.Equal("HELD", hold.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Unknown_hold_is_not_found_before_ownership_is_checked()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var missing = Guid.NewGuid().ToString();

        var confirm = await ApiCalls.ConfirmAsync(client, missing, ApiCalls.NewStudentId());
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
        Assert.Equal("HOLD_NOT_FOUND", await ApiCalls.ErrorCodeAsync(confirm));

        var cancel = await ApiCalls.CancelAsync(client, missing, ApiCalls.NewStudentId());
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal("HOLD_NOT_FOUND", await ApiCalls.ErrorCodeAsync(cancel));
    }

    [Fact]
    public async Task Cancelled_hold_cannot_be_confirmed()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 2);
        var student = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        Assert.Equal(HttpStatusCode.NoContent, (await ApiCalls.CancelAsync(client, holdId, student)).StatusCode);

        var confirm = await ApiCalls.ConfirmAsync(client, holdId, student);
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("HOLD_CANCELLED", await ApiCalls.ErrorCodeAsync(confirm));
    }
}
