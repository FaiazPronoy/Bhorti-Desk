using Dapper;
using Npgsql;

namespace SchoolAdmission.Api.Services;

// Every few seconds: offer freed seats to the waiting list, and write the "two minutes
// left" and "expired" notices. With two backends, only the one that gets the PostgreSQL
// advisory lock does a round; the other skips it. Correctness never depends on this job:
// applying and reading the waiting list make offers too, and expiry is worked out on read.
public class SeasonWorker(IServiceScopeFactory scopes, NpgsqlDataSource dataSource, ILogger<SeasonWorker> logger) : BackgroundService
{
    private const long WorkerLockKey = 20261124;
    public static TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "Season worker round failed; it will try again");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // A session lock: held only while this round runs, on this one connection.
        if (!await connection.ExecuteScalarAsync<bool>("select pg_try_advisory_lock(@key)", new { key = WorkerLockKey })) return 0;
        try
        {
            using var scope = scopes.CreateScope();
            var waitlist = scope.ServiceProvider.GetRequiredService<WaitlistService>();
            var notices = scope.ServiceProvider.GetRequiredService<NoticeService>();

            var classIds = await connection.QueryAsync<Guid>(
                "select distinct admission_class_id from waitlist_entries where status = 'WAITING'");
            var offered = 0;
            foreach (var classId in classIds) offered += await waitlist.OfferForClassAsync(connection, classId);
            if (offered > 0)
            {
                SeasonMetrics.AddOffers(offered);
                logger.LogInformation("Offered {Count} freed seats from waiting lists", offered);
            }

            await notices.WriteTimedNoticesAsync(connection);
            await notices.SendPendingSmsAsync(connection);
            return offered;
        }
        finally
        {
            await connection.ExecuteAsync("select pg_advisory_unlock(@key)", new { key = WorkerLockKey });
        }
    }
}
