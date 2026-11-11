using System.Net;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class SeatHoldExpiryTests
{
    [Fact]
    public async Task Hold_expires_by_itself_and_the_seat_returns()
    {
        // A two second hold keeps the test short. Nothing runs in the background to
        // expire it: the next read or the next application has to notice on its own.
        await using var api = new AdmissionApiFactory(holdSeconds: 2);
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 1);
        var firstStudent = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, firstStudent);

        var full = await ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId());
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Equal("CLASS_FULL", await ApiCalls.ErrorCodeAsync(full));

        await Task.Delay(TimeSpan.FromSeconds(3.5));

        var hold = await ApiCalls.GetJsonAsync(client, $"/holds/{holdId}");
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(0, seats.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, seats.GetProperty("availableSeats").GetInt32());

        var lateConfirm = await ApiCalls.ConfirmAsync(client, holdId, firstStudent);
        Assert.Equal(HttpStatusCode.Conflict, lateConfirm.StatusCode);
        Assert.Equal("HOLD_EXPIRED", await ApiCalls.ErrorCodeAsync(lateConfirm));

        var secondTry = await ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId());
        Assert.Equal(HttpStatusCode.Created, secondTry.StatusCode);
    }

    [Fact]
    public async Task Student_can_apply_again_after_their_own_hold_expires()
    {
        await using var api = new AdmissionApiFactory(holdSeconds: 2);
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 3);
        var student = ApiCalls.NewStudentId();
        var firstHold = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);

        await Task.Delay(TimeSpan.FromSeconds(3.5));

        var secondHold = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
        Assert.NotEqual(firstHold, secondHold);

        var mine = await ApiCalls.MyHoldsAsync(client, student);
        var statuses = mine.EnumerateArray()
            .ToDictionary(h => h.GetProperty("holdId").GetString()!, h => h.GetProperty("status").GetString());
        Assert.Equal("EXPIRED", statuses[firstHold]);
        Assert.Equal("HELD", statuses[secondHold]);
    }
}
