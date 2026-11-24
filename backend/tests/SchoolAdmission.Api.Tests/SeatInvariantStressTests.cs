using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

// Hard tests for the one rule that matters most: a seat never goes to two students.
// After each run the database itself is checked with plain SQL, not only the API answers.
public class SeatInvariantStressTests
{
    // Every class where held + confirmed is more than its seats. Must always be empty.
    private const string OverbookedClassesSql = """
        select c.id from admission_classes c
         where (select count(*) from seat_holds h
                 where h.admission_class_id = c.id
                   and (h.status = 'CONFIRMED' or (h.status = 'HELD' and h.expires_at > clock_timestamp())))
               > c.total_seats
        """;

    // Every student with more than one active hold or admission in one class. Must be empty.
    private const string DoubleBookedStudentsSql = """
        select student_birth_registration_number, admission_class_id from seat_holds
         where status = 'CONFIRMED' or (status = 'HELD' and expires_at > clock_timestamp())
         group by student_birth_registration_number, admission_class_id
        having count(*) > 1
        """;

    private const string ConfirmedPerClassOverLimitSql = """
        select c.id from admission_classes c
         where (select count(*) from seat_holds h where h.admission_class_id = c.id and h.status = 'CONFIRMED') > c.total_seats
        """;

    private static async Task AssertDatabaseIsConsistentAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var (name, sql) in new[]
                 {
                     ("overbooked classes", OverbookedClassesSql),
                     ("students with two active holds in one class", DoubleBookedStudentsSql),
                     ("classes with more confirmed seats than seats", ConfirmedPerClassOverLimitSql),
                 })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.False(await reader.ReadAsync(), $"Found {name}");
        }
    }

    [Fact]
    public async Task Crowd_with_repeat_clicks_on_two_backends_never_double_books()
    {
        await using var first = new AdmissionApiFactory();
        await using var second = new AdmissionApiFactory(connectionString: first.ConnectionString);
        var clients = new[] { first.CreateClient(), second.CreateClient() };

        int[] seatCounts = [1, 2, 7, 25];
        var classIds = new List<string>();
        foreach (var seats in seatCounts)
        {
            classIds.Add(await ApiCalls.CreateClassAsync(clients[0], seats, $"Class {seats}"));
        }

        // 150 students. Each one clicks Apply three times on two random classes, on random
        // backends, all at once: 900 requests in flight together.
        var students = Enumerable.Range(0, 150).Select(_ => ApiCalls.NewStudentId()).ToList();
        var random = new Random(470);
        var requests = new List<Task<HttpResponseMessage>>();
        foreach (var student in students)
        {
            foreach (var classId in classIds.OrderBy(_ => random.Next()).Take(2))
            {
                for (var click = 0; click < 3; click++)
                {
                    requests.Add(ApiCalls.ApplyAsync(clients[random.Next(2)], classId, student));
                }
            }
        }
        var responses = await Task.WhenAll(requests);

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));

        // Every class is now exactly full, with every seat held by a different student.
        for (var i = 0; i < classIds.Count; i++)
        {
            var seats = await ApiCalls.GetJsonAsync(clients[1], $"/classes/{classIds[i]}");
            Assert.Equal(seatCounts[i], seats.GetProperty("heldSeats").GetInt32());
            Assert.Equal(0, seats.GetProperty("availableSeats").GetInt32());
        }
        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(seatCounts.Sum(), created);

        await AssertDatabaseIsConsistentAsync(first.ConnectionString);
    }

    [Fact]
    public async Task Paying_at_the_expiry_second_never_gives_the_seat_to_two_students()
    {
        // One seat and a one second hold. The owner keeps pressing Pay while a crowd keeps
        // applying, across the exact moment the hold runs out. Whoever wins, the seat must
        // end up with one student only.
        for (var round = 0; round < 3; round++)
        {
            await using var api = new AdmissionApiFactory(holdSeconds: 1);
            var client = api.CreateClient();
            var classId = await ApiCalls.CreateClassAsync(client, 1);
            var owner = ApiCalls.NewStudentId();
            var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, owner);

            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            var results = new List<HttpResponseMessage>();
            var resultsLock = new object();

            async Task KeepSending(Func<Task<HttpResponseMessage>> send)
            {
                while (!stop.IsCancellationRequested)
                {
                    var response = await send();
                    lock (resultsLock) results.Add(response);
                }
            }

            var workers = new List<Task> { KeepSending(() => ApiCalls.ConfirmAsync(client, holdId, owner)) };
            workers.AddRange(Enumerable.Range(0, 20)
                .Select(_ => KeepSending(() => ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId()))));
            await Task.WhenAll(workers);

            Assert.DoesNotContain(results, r => (int)r.StatusCode >= 500);
            await AssertDatabaseIsConsistentAsync(api.ConnectionString);

            var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
            Assert.True(seats.GetProperty("heldSeats").GetInt32() + seats.GetProperty("confirmedSeats").GetInt32() <= 1);
        }
    }

    [Fact]
    public async Task Cancel_and_reapply_storm_keeps_the_count_right()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateClient();
        const int totalSeats = 3;
        var classId = await ApiCalls.CreateClassAsync(client, totalSeats);

        // Ten students each apply, cancel and apply again five times, all at once.
        async Task Churn(string student)
        {
            for (var i = 0; i < 5; i++)
            {
                var response = await ApiCalls.ApplyAsync(client, classId, student);
                Assert.True((int)response.StatusCode < 500);
                if (response.StatusCode != HttpStatusCode.Created) continue;
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                var cancel = await ApiCalls.CancelAsync(client, body.GetProperty("holdId").GetString()!, student);
                Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Churn(ApiCalls.NewStudentId())));

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(0, seats.GetProperty("heldSeats").GetInt32());
        Assert.Equal(totalSeats, seats.GetProperty("availableSeats").GetInt32());
        await AssertDatabaseIsConsistentAsync(api.ConnectionString);
    }

    [Fact]
    public async Task Data_written_before_a_restart_is_still_there_after_it()
    {
        // Durability across a restart of the API: a new host on the same database sees
        // the same classes and holds.
        string classId, holdId, student = ApiCalls.NewStudentId();
        string connectionString;
        await using (var before = new AdmissionApiFactory())
        {
            var client = before.CreateClient();
            connectionString = before.ConnectionString;
            classId = await ApiCalls.CreateClassAsync(client, 4);
            holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
            Assert.Equal(HttpStatusCode.OK, (await ApiCalls.ConfirmAsync(client, holdId, student)).StatusCode);
        }

        await using var after = new AdmissionApiFactory(connectionString: connectionString);
        var restarted = after.CreateClient();
        var seats = await ApiCalls.GetJsonAsync(restarted, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("confirmedSeats").GetInt32());
        var hold = await ApiCalls.GetJsonAsync(restarted, $"/holds/{holdId}");
        Assert.Equal("CONFIRMED", hold.GetProperty("status").GetString());
    }
}
