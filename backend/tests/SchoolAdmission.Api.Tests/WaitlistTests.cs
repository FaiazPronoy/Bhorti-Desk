using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Api.Services;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class WaitlistTests
{
    private static Task<HttpResponseMessage> JoinAsync(HttpClient client, string classId, string student)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/waitlist");
        request.Headers.Add("X-Student-Id", student);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> MyWaitlistAsync(HttpClient client, string student)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/me/waitlist");
        request.Headers.Add("X-Student-Id", student);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Joining_needs_a_full_class()
    {
        await using var api = new AdmissionApiFactory(workerSeconds: 0);
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 1);

        var tooEarly = await JoinAsync(client, classId, ApiCalls.NewStudentId());
        Assert.Equal("SEATS_STILL_FREE", await ApiCalls.ErrorCodeAsync(tooEarly));

        await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, ApiCalls.NewStudentId());
        var student = ApiCalls.NewStudentId();
        Assert.Equal(HttpStatusCode.Created, (await JoinAsync(client, classId, student)).StatusCode);
        Assert.Equal("ALREADY_WAITING", await ApiCalls.ErrorCodeAsync(await JoinAsync(client, classId, student)));
    }

    [Fact]
    public async Task A_freed_seat_goes_to_the_first_in_line_even_with_the_worker_asleep()
    {
        await using var api = new AdmissionApiFactory(workerSeconds: 0);
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 1);
        var holder = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, holder);
        var first = ApiCalls.NewStudentId();
        var second = ApiCalls.NewStudentId();
        await JoinAsync(client, classId, first);
        await JoinAsync(client, classId, second);

        await ApiCalls.CancelAsync(client, holdId, holder);

        // A walk-in applicant cannot jump the line: the seat is offered first.
        var walkIn = await ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId());
        Assert.Equal("CLASS_FULL", await ApiCalls.ErrorCodeAsync(walkIn));

        var firstPlace = Assert.Single((await MyWaitlistAsync(client, first)).EnumerateArray());
        Assert.Equal("OFFERED", firstPlace.GetProperty("status").GetString());
        var offeredHold = firstPlace.GetProperty("offeredHoldId").GetString()!;
        Assert.Equal("HELD", (await ApiCalls.GetJsonAsync(client, $"/holds/{offeredHold}")).GetProperty("status").GetString());

        var secondPlace = Assert.Single((await MyWaitlistAsync(client, second)).EnumerateArray());
        Assert.Equal("WAITING", secondPlace.GetProperty("status").GetString());
        Assert.Equal(1, secondPlace.GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task The_worker_makes_offers_and_writes_the_offer_notice()
    {
        await using var api = new AdmissionApiFactory(workerSeconds: 0);
        var client = api.CreateAnonymousClient();
        var classId = await ApiCalls.CreateClassAsync(client, 1);
        var holder = ApiCalls.NewStudentId();
        var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, holder);
        var waiting = ApiCalls.NewStudentId();
        await JoinAsync(client, classId, waiting);
        await ApiCalls.CancelAsync(client, holdId, holder);

        var worker = ActivatorUtilities.CreateInstance<SeasonWorker>(api.Services);
        Assert.Equal(1, await worker.RunOnceAsync());

        var request = new HttpRequestMessage(HttpMethod.Get, "/me/notices");
        request.Headers.Add("X-Student-Id", waiting);
        var notices = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(notices.EnumerateArray(), notice => notice.GetProperty("kind").GetString() == "OFFERED");
    }

    [Fact]
    public async Task Many_releases_and_joins_at_once_never_overfill_and_keep_the_order()
    {
        await using var first = new AdmissionApiFactory(workerSeconds: 0);
        await using var second = new AdmissionApiFactory(connectionString: first.ConnectionString, workerSeconds: 0);
        var clients = new[] { first.CreateAnonymousClient(), second.CreateAnonymousClient() };
        const int seats = 10;
        var classId = await ApiCalls.CreateClassAsync(clients[0], seats);
        var holders = new List<(string Student, string HoldId)>();
        for (var i = 0; i < seats; i++)
        {
            var student = ApiCalls.NewStudentId();
            holders.Add((student, await ApiCalls.ApplyAndGetHoldIdAsync(clients[0], classId, student)));
        }

        // Thirty people join in a known order, then everything happens at once: ten holders
        // cancel, forty walk-ins apply, both workers run, and more people join.
        var line = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            var student = ApiCalls.NewStudentId();
            Assert.Equal(HttpStatusCode.Created, (await JoinAsync(clients[i % 2], classId, student)).StatusCode);
            line.Add(student);
        }
        var workers = new[] { first, second }.Select(api => ActivatorUtilities.CreateInstance<SeasonWorker>(api.Services)).ToArray();
        var storm = new List<Task>();
        var cancels = holders.Select((holder, i) => ApiCalls.CancelAsync(clients[i % 2], holder.HoldId, holder.Student)).ToList();
        storm.AddRange(cancels);
        storm.AddRange(Enumerable.Range(0, 40).Select(i => ApiCalls.ApplyAsync(clients[i % 2], classId, ApiCalls.NewStudentId())));
        storm.AddRange(Enumerable.Range(0, 6).Select(i => workers[i % 2].RunOnceAsync()));
        storm.AddRange(Enumerable.Range(0, 20).Select(i => JoinAsync(clients[i % 2], classId, ApiCalls.NewStudentId())));
        await Task.WhenAll(storm);
        foreach (var cancel in cancels) Assert.Equal(HttpStatusCode.NoContent, (await cancel).StatusCode);
        await workers[0].RunOnceAsync();

        await using var connection = new NpgsqlConnection(first.ConnectionString);
        await connection.OpenAsync();
        await using (var overfill = new NpgsqlCommand("""
            select count(*) from seat_holds where admission_class_id = @classId
               and (status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp()))
            """, connection))
        {
            overfill.Parameters.AddWithValue("classId", Guid.Parse(classId));
            Assert.Equal((long)seats, await overfill.ExecuteScalarAsync());
        }

        // The ten offers went to the first ten in line, and nobody further back got one.
        await using var offered = new NpgsqlCommand("""
            select student_birth_registration_number from waitlist_entries
             where admission_class_id = @classId and status = 'OFFERED' order by id
            """, connection);
        offered.Parameters.AddWithValue("classId", Guid.Parse(classId));
        var offeredStudents = new List<string>();
        await using (var reader = await offered.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) offeredStudents.Add(reader.GetString(0));
        }
        if (!line.Take(seats).SequenceEqual(offeredStudents))
        {
            // Who holds the seats, to see where the line was jumped.
            await using var who = new NpgsqlCommand("""
                select h.student_birth_registration_number || ' ' || h.status || ' ' || coalesce(w.status, 'no-entry') || ' ' || coalesce(w.id::text, '')
                  from seat_holds h left join waitlist_entries w on w.offered_hold_id = h.id
                 where h.admission_class_id = @classId and h.status = 'HELD' order by h.created_at
                """, connection);
            who.Parameters.AddWithValue("classId", Guid.Parse(classId));
            var holders2 = new List<string>();
            await using (var reader = await who.ExecuteReaderAsync()) while (await reader.ReadAsync()) holders2.Add(reader.GetString(0));
            var lineIndex = string.Join(",", holders2.Select(h => line.IndexOf(h[..17])));
            Assert.Fail($"Offers out of order. Seat holders: {string.Join(" | ", holders2)}. Their place in line: {lineIndex}");
        }
    }
}
