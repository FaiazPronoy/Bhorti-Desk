using System.Net;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class SeatHoldConcurrencyTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(1)]
    public async Task Many_students_apply_at_once_and_holds_never_exceed_seats(int totalSeats)
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();

        // Five rounds, each on a new class, so a lucky run cannot hide a race.
        for (var round = 1; round <= 5; round++)
        {
            var classId = await ApiCalls.CreateClassAsync(client, totalSeats);

            var responses = await Task.WhenAll(Enumerable.Range(0, 300)
                .Select(_ => ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId())));

            var codes = responses.Select(r => (int)r.StatusCode).ToList();
            Assert.DoesNotContain(codes, code => code >= 500);
            Assert.Equal(totalSeats, codes.Count(code => code == 201));
            Assert.Equal(300 - totalSeats, codes.Count(code => code == 409));

            var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
            Assert.Equal(totalSeats, seats.GetProperty("heldSeats").GetInt32());
            Assert.Equal(0, seats.GetProperty("confirmedSeats").GetInt32());
            Assert.Equal(0, seats.GetProperty("availableSeats").GetInt32());
        }
    }

    [Fact]
    public async Task Same_student_applying_twice_at_once_gets_one_hold()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        var classId = await ApiCalls.CreateClassAsync(client, 50);
        var studentId = ApiCalls.NewStudentId();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => ApiCalls.ApplyAsync(client, classId, studentId)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var rejected in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            Assert.Equal("ALREADY_APPLIED", await ApiCalls.ErrorCodeAsync(rejected));
        }

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Mixed_actions_never_exceed_seats()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        const int totalSeats = 5;
        var classId = await ApiCalls.CreateClassAsync(client, totalSeats);

        // Fill the class, then confirm some, cancel some and send a crowd of new
        // applications, all at the same time.
        var students = Enumerable.Range(0, totalSeats).Select(_ => ApiCalls.NewStudentId()).ToList();
        var holdIds = new List<string>();
        foreach (var student in students)
        {
            holdIds.Add(await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student));
        }

        var work = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < totalSeats; i++)
        {
            work.Add(i % 2 == 0
                ? ApiCalls.ConfirmAsync(client, holdIds[i], students[i])
                : ApiCalls.CancelAsync(client, holdIds[i], students[i]));
        }
        work.AddRange(Enumerable.Range(0, 100).Select(_ => ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId())));
        var responses = await Task.WhenAll(work);

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        var taken = seats.GetProperty("heldSeats").GetInt32() + seats.GetProperty("confirmedSeats").GetInt32();
        Assert.True(taken <= totalSeats, $"held + confirmed is {taken}, more than {totalSeats} seats");
        Assert.True(seats.GetProperty("availableSeats").GetInt32() >= 0);
    }
}
