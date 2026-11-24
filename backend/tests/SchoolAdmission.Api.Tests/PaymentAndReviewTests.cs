using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using SchoolAdmission.Api.Tests.TestSupport;

namespace SchoolAdmission.Api.Tests;

public class PaymentAndReviewTests
{
    private const string LocalSecret = "local-only-secret-change-me";

    private static async Task<(string PaymentId, int Amount)> StartAsync(HttpClient client, string holdId, string student, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/applications/{holdId}/payments");
        request.Headers.Add("X-Student-Id", student);
        request.Headers.Add("Idempotency-Key", key);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("paymentId").GetString()!, body.GetProperty("amountTaka").GetInt32());
    }

    private static Task<HttpResponseMessage> CallbackAsync(HttpClient client, string body, string secret = LocalSecret)
    {
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
        var request = new HttpRequestMessage(HttpMethod.Post, "/payments/callback") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Signature", signature);
        return client.SendAsync(request);
    }

    private static async Task<(AdmissionApiFactory Api, HttpClient Client, string ClassId, string HoldId, string Student)> HoldWithFeeAsync(int seats = 3, int holdSeconds = 600)
    {
        var api = new AdmissionApiFactory(holdSeconds: holdSeconds);
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, seats);
        Assert.Equal(HttpStatusCode.OK, (await staff.PutAsJsonAsync($"/office/classes/{classId}/rules", new { feeTaka = 1500 })).StatusCode);
        var student = ApiCalls.NewStudentId();
        return (api, client, classId, await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student), student);
    }

    private static async Task<long> CountAsync(AdmissionApiFactory api, string sql, string holdId)
    {
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", Guid.Parse(holdId));
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task The_sandbox_pays_and_the_hold_becomes_confirmed()
    {
        var (api, client, _, holdId, student) = await HoldWithFeeAsync();
        await using var _api = api;
        var (paymentId, amount) = await StartAsync(client, holdId, student, "key-1");
        Assert.Equal(1500, amount);

        var decide = await client.PostAsJsonAsync($"/sandbox-gateway/payments/{paymentId}/decide", new { outcome = "PAID" });
        Assert.Equal(HttpStatusCode.OK, decide.StatusCode);

        var hold = await ApiCalls.GetJsonAsync(client, $"/holds/{holdId}");
        Assert.Equal("CONFIRMED", hold.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_double_click_and_a_repeated_callback_pay_once()
    {
        var (api, client, _, holdId, student) = await HoldWithFeeAsync();
        await using var _api = api;

        var starts = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => StartAsync(client, holdId, student, "same-key")));
        Assert.Single(starts.Select(start => start.PaymentId).Distinct());

        var body = JsonSerializer.Serialize(new { paymentId = starts[0].PaymentId, status = "PAID", amountTaka = 1500 });
        var callbacks = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => CallbackAsync(client, body)));
        Assert.All(callbacks, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        Assert.Equal(1, await CountAsync(api, "select count(*) from payments where seat_hold_id = @id and status = 'PAID'", holdId));
        Assert.Equal(1, await CountAsync(api, "select count(*) from notices where seat_hold_id = @id and kind = 'PAID'", holdId));
    }

    [Fact]
    public async Task A_callback_with_a_wrong_signature_or_amount_changes_nothing()
    {
        var (api, client, _, holdId, student) = await HoldWithFeeAsync();
        await using var _api = api;
        var (paymentId, _) = await StartAsync(client, holdId, student, "key-2");

        var forged = await CallbackAsync(client, JsonSerializer.Serialize(new { paymentId, status = "PAID", amountTaka = 1500 }), "guessed-secret");
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("BAD_SIGNATURE", await ApiCalls.ErrorCodeAsync(forged));

        var cheap = await CallbackAsync(client, JsonSerializer.Serialize(new { paymentId, status = "PAID", amountTaka = 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, cheap.StatusCode);

        Assert.Equal("HELD", (await ApiCalls.GetJsonAsync(client, $"/holds/{holdId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Paying_after_the_hold_ran_out_is_marked_late_and_takes_no_seat()
    {
        var (api, client, classId, holdId, student) = await HoldWithFeeAsync(seats: 1, holdSeconds: 2);
        await using var _api = api;
        var (paymentId, _) = await StartAsync(client, holdId, student, "key-late");
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        // Someone else takes the freed seat before the late payment arrives.
        await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, ApiCalls.NewStudentId());

        var decide = await client.PostAsJsonAsync($"/sandbox-gateway/payments/{paymentId}/decide", new { outcome = "PAID" });
        Assert.Equal("LATE", (await decide.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("heldSeats").GetInt32() + seats.GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task Rejecting_frees_the_seat_and_marks_a_refund_due()
    {
        var (api, client, classId, holdId, student) = await HoldWithFeeAsync(seats: 1);
        await using var _api = api;
        var (paymentId, _) = await StartAsync(client, holdId, student, "key-3");
        await client.PostAsJsonAsync($"/sandbox-gateway/payments/{paymentId}/decide", new { outcome = "PAID" });
        var staff = api.CreateSignedInClient();

        var rejected = await staff.PostAsJsonAsync($"/office/applications/{holdId}/reject", new { reason = "Birth certificate does not match" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var review = (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("review");
        Assert.True(review.GetProperty("refundDue").GetBoolean());

        var seats = await ApiCalls.GetJsonAsync(client, $"/classes/{classId}");
        Assert.Equal(1, seats.GetProperty("availableSeats").GetInt32());
        Assert.Equal(HttpStatusCode.Created, (await ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId())).StatusCode);

        var again = await staff.PostAsJsonAsync($"/office/applications/{holdId}/verify", new { });
        Assert.Equal("ALREADY_REVIEWED", await ApiCalls.ErrorCodeAsync(again));
    }

    [Fact]
    public async Task Rejections_and_applications_at_once_never_overfill_a_class()
    {
        await using var api = new AdmissionApiFactory();
        var client = api.CreateAnonymousClient();
        var staff = api.CreateSignedInClient();
        var classId = await ApiCalls.CreateClassAsync(client, 5);
        var paid = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var student = ApiCalls.NewStudentId();
            var holdId = await ApiCalls.ApplyAndGetHoldIdAsync(client, classId, student);
            Assert.Equal(HttpStatusCode.OK, (await ApiCalls.ConfirmAsync(client, holdId, student)).StatusCode);
            paid.Add(holdId);
        }

        var rejections = paid.Select(holdId => staff.PostAsJsonAsync($"/office/applications/{holdId}/reject", new { reason = "Test rejection" }));
        var applications = Enumerable.Range(0, 60).Select(_ => ApiCalls.ApplyAsync(client, classId, ApiCalls.NewStudentId()));
        await Task.WhenAll(rejections.Concat(applications));

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
    public async Task The_office_review_list_needs_staff()
    {
        await using var api = new AdmissionApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateAnonymousClient().GetAsync("/office/applications")).StatusCode);
    }
}
