using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class TwoBackendInstancesTests
{
    [Fact]
    public async Task Two_backends_on_one_database_still_never_exceed_seats()
    {
        // Two separate API instances, each with its own connection pool, sharing one
        // database. Nothing is kept in memory, so the row lock is the only guard.
        await using var first = new AdmissionApiFactory();
        await using var second = new AdmissionApiFactory(connectionString: first.ConnectionString);
        var firstClient = first.CreateClient();
        var secondClient = second.CreateClient();

        const int totalSeats = 7;
        var classId = await ApiCalls.CreateClassAsync(firstClient, totalSeats);

        var responses = await Task.WhenAll(Enumerable.Range(0, 200).Select(i =>
            ApiCalls.ApplyAsync(i % 2 == 0 ? firstClient : secondClient, classId, ApiCalls.NewStudentId())));

        var codes = responses.Select(r => (int)r.StatusCode).ToList();
        Assert.DoesNotContain(codes, code => code >= 500);
        Assert.Equal(totalSeats, codes.Count(code => code == 201));

        var seen = await ApiCalls.GetJsonAsync(secondClient, $"/classes/{classId}");
        Assert.Equal(totalSeats, seen.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, seen.GetProperty("availableSeats").GetInt32());
    }
}
