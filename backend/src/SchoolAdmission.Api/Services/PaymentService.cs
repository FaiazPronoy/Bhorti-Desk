using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Payments;

namespace SchoolAdmission.Api.Services;

public class PaymentService(AdmissionDatabase database, SandboxGateway gateway, Notices notices, ILogger<PaymentService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Step 1: the guardian presses Pay. A repeated press with the same key returns the
    // same payment instead of starting a second one.
    public async Task<PaymentStartResponse> StartAsync(Guid holdId, string studentId, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 80)
        {
            throw new FieldValidationException(new() { ["idempotencyKey"] = "Send an Idempotency-Key header of up to 80 characters." });
        }

        await using var connection = await database.OpenConnectionAsync();
        var existing = await connection.QuerySingleOrDefaultAsync<(Guid Id, Guid HoldId, int Amount, string Status)>(
            "select id, seat_hold_id, amount_taka, status from payments where idempotency_key = @idempotencyKey", new { idempotencyKey });
        if (existing.Id != Guid.Empty)
        {
            if (existing.HoldId != holdId) throw AdmissionRuleException.PaymentKeyReused();
            await RequireOwnerAsync(connection, holdId, studentId);
            return new PaymentStartResponse(existing.Id, existing.Amount, gateway.Name, existing.Status, gateway.HostedPageUrl(existing.Id));
        }

        var hold = await RequireOwnerAsync(connection, holdId, studentId);
        if (hold.Status == SeatHoldStatus.Confirmed) throw AdmissionRuleException.AlreadyPaid();
        if (hold.Status == SeatHoldStatus.Expired) throw AdmissionRuleException.Expired();
        if (hold.Status == SeatHoldStatus.Cancelled) throw AdmissionRuleException.Cancelled();

        var id = Guid.CreateVersion7();
        try
        {
            await connection.ExecuteAsync("""
                insert into payments (id, seat_hold_id, amount_taka, gateway, idempotency_key)
                values (@id, @holdId, @amount, @gateway, @idempotencyKey)
                """, new { id, holdId, amount = hold.FeeTaka, gateway = gateway.Name, idempotencyKey });
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // The same key arrived twice at the same moment. The other request won; use its row.
            return await StartAsync(holdId, studentId, idempotencyKey);
        }
        return new PaymentStartResponse(id, hold.FeeTaka, gateway.Name, "PENDING", gateway.HostedPageUrl(id));
    }

    // The sandbox's own hosted page reads this. It is the gateway's page, not ours, so it
    // shows only what a gateway would know.
    public async Task<SandboxPageResponse> SandboxPageAsync(Guid paymentId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<SandboxRow>("""
            select p.id as payment_id, p.seat_hold_id as hold_id, c.name as class_name, h.serial_number, h.created_at,
                   p.amount_taka, p.status
              from payments p join seat_holds h on h.id = p.seat_hold_id join admission_classes c on c.id = h.admission_class_id
             where p.id = @paymentId
            """, new { paymentId }) ?? throw AdmissionRuleException.PaymentNotFound();
        return new SandboxPageResponse(row.PaymentId, row.HoldId, row.ClassName, Serials.Format(row.CreatedAt, row.SerialNumber), row.AmountTaka, row.Status);
    }

    // The guardian presses "Pay" or "Fail" on the sandbox page. The sandbox then sends the
    // signed callback, exactly as a real gateway would call our callback address.
    public async Task<string> SandboxDecideAsync(Guid paymentId, string? outcome)
    {
        var status = outcome?.ToUpperInvariant() switch
        {
            "PAID" => "PAID",
            "FAILED" => "FAILED",
            _ => throw new FieldValidationException(new() { ["outcome"] = "Choose PAID or FAILED." }),
        };
        var page = await SandboxPageAsync(paymentId);
        var body = JsonSerializer.Serialize(new PaymentCallback(paymentId, status, page.AmountTaka), Json);
        return await HandleCallbackAsync(body, gateway.Sign(body));
    }

    // Step 3: the signed callback. Safe to receive many times: once a payment is finished,
    // a repeat changes nothing.
    public async Task<string> HandleCallbackAsync(string body, string? signature)
    {
        SeasonMetrics.PaymentCallback();
        if (!gateway.IsGenuine(body, signature)) throw AdmissionRuleException.BadSignature();
        var callback = JsonSerializer.Deserialize<PaymentCallback>(body, Json) ?? throw AdmissionRuleException.BadSignature();

        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await connection.ExecuteAsync("set local lock_timeout = '20s'", transaction: transaction);

        // The class lock first, then the payment row: the same order as applying, so no deadlock.
        var holdId = await connection.ExecuteScalarAsync<Guid?>("""
            select p.seat_hold_id from payments p
              join seat_holds h on h.id = p.seat_hold_id
              join admission_classes c on c.id = h.admission_class_id
             where p.id = @PaymentId
               for update of c
            """, callback, transaction) ?? throw AdmissionRuleException.PaymentNotFound();
        var payment = await connection.QuerySingleAsync<(string Status, int Amount)>(
            "select status, amount_taka from payments where id = @PaymentId for update", callback, transaction);

        if (payment.Status != "PENDING")
        {
            await transaction.CommitAsync();
            return payment.Status;
        }
        if (callback.AmountTaka != payment.Amount) throw AdmissionRuleException.BadSignature();

        var result = callback.Status == "PAID" ? "PAID" : "FAILED";
        if (result == "PAID")
        {
            var confirmed = await connection.ExecuteAsync("""
                update seat_holds set status = 'CONFIRMED', confirmed_at = clock_timestamp()
                 where id = @holdId and status = 'HELD' and expires_at > clock_timestamp()
                """, new { holdId }, transaction);
            // Paid after the time ran out: the seat may already be someone else's. Keep the
            // money record honest and mark it for a refund instead of confirming.
            if (confirmed == 0) result = "LATE";
        }

        await connection.ExecuteAsync(
            "update payments set status = @result, finished_at = clock_timestamp() where id = @PaymentId",
            new { result, callback.PaymentId }, transaction);
        if (result == "PAID") await notices.AddAsync(connection, transaction, holdId, Notices.Paid);
        await transaction.CommitAsync();

        logger.LogInformation("Payment {PaymentId} for hold {HoldId}: {Result}", callback.PaymentId, holdId, result);
        return result;
    }

    private static async Task<HoldForPayment> RequireOwnerAsync(NpgsqlConnection connection, Guid holdId, string studentId)
    {
        var hold = await connection.QuerySingleOrDefaultAsync<HoldForPayment>($"""
            select h.student_birth_registration_number as student_id, {SeatHoldStatus.EffectiveStatusSql} as status,
                   coalesce(r.fee_taka, 0) as fee_taka
              from seat_holds h left join class_rules r on r.admission_class_id = h.admission_class_id
             where h.id = @holdId
            """, new { holdId }) ?? throw AdmissionRuleException.HoldNotFound();
        if (hold.StudentId != studentId) throw AdmissionRuleException.NotOwned();
        return hold;
    }

    private class HoldForPayment
    {
        public string StudentId { get; set; } = "";
        public string Status { get; set; } = "";
        public int FeeTaka { get; set; }
    }

    private class SandboxRow
    {
        public Guid PaymentId { get; set; }
        public Guid HoldId { get; set; }
        public string ClassName { get; set; } = "";
        public long SerialNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AmountTaka { get; set; }
        public string Status { get; set; } = "";
    }
}
