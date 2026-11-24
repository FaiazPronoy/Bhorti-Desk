using Dapper;
using Npgsql;
using SchoolAdmission.Api.Configuration;

namespace SchoolAdmission.Api.Services;

// In a public demo, wipe everything people typed once a day, after midnight in Dhaka.
// The first school office account stays. Only runs with DEMO_MODE=true.
public class DemoReset(NpgsqlDataSource dataSource, AdmissionSettings settings, ILogger<DemoReset> logger) : BackgroundService
{
    private const long ResetLockKey = 20261125;
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.DemoMode) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResetIfNewDayAsync(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Dhaka)));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "Demo reset check failed; it will try again");
            }
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    // True when this call did the reset. Safe with two backends: one takes the lock, and
    // the day is written down so the reset happens once.
    public async Task<bool> ResetIfNewDayAsync(DateOnly today)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("select pg_advisory_xact_lock(@key)", new { key = ResetLockKey }, transaction);
        await connection.ExecuteAsync("create table if not exists demo_resets (day date primary key)", transaction: transaction);
        var done = await connection.ExecuteScalarAsync<bool>(
            "select exists (select 1 from demo_resets where day = @today)", new { today = today.ToDateTime(TimeOnly.MinValue) }, transaction);
        if (done) return false;

        await connection.ExecuteAsync("""
            truncate notices, waitlist_entries, application_reviews, payments, application_documents, applicant_details,
                     seat_holds, class_rules, admission_classes, admission_sessions, students, guardians, otp_codes,
                     user_sessions, audit_log restart identity cascade
            """, transaction: transaction);
        await connection.ExecuteAsync("insert into demo_resets (day) values (@today)", new { today = today.ToDateTime(TimeOnly.MinValue) }, transaction);
        await transaction.CommitAsync();
        logger.LogInformation("Demo data reset for {Day}", today);
        return true;
    }
}
