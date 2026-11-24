using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Tests.TestSupport;

// Starts the real API in memory against a real PostgreSQL database. Locks cannot be
// tested against a fake database, so every factory gets a brand new database of its own.
public class AdmissionApiFactory : WebApplicationFactory<Program>
{
    private readonly int holdSeconds;
    private readonly string? adminEmail;
    private readonly string? adminPassword;
    private readonly int? cooldownSeconds;
    private readonly bool rateLimits;
    private readonly bool signInRequired;
    private readonly int? workerSeconds;
    private readonly bool demoMode;

    public string ConnectionString { get; }

    // Pass a connection string to put a second API process on the same database.
    // Anything not passed is left unset, so the original contract tests really run on the
    // app's default settings.
    public AdmissionApiFactory(int holdSeconds = 600, string? connectionString = null,
        string? adminEmail = null, string? adminPassword = null, int? cooldownSeconds = null, bool rateLimits = false,
        bool signInRequired = false, int? workerSeconds = null, bool demoMode = false)
    {
        this.demoMode = demoMode;
        this.workerSeconds = workerSeconds;
        this.cooldownSeconds = cooldownSeconds;
        this.rateLimits = rateLimits;
        this.signInRequired = signInRequired;
        this.holdSeconds = holdSeconds;
        this.adminEmail = adminEmail;
        this.adminPassword = adminPassword;
        ownsDatabase = connectionString is null;
        ConnectionString = connectionString ?? CreateFreshDatabase();
    }

    // Only the factory that made the database drops it. A second backend on the same
    // database leaves it alone.
    private readonly bool ownsDatabase;

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
        if (!ownsDatabase) return;

        // The restart test stops one host and starts another on the same database, so a
        // finished database is not dropped at once. Only the oldest ones are, which keeps
        // the throwaway PostgreSQL from filling its memory.
        RetiredDatabases.Enqueue(new NpgsqlConnectionStringBuilder(ConnectionString).Database!);
        while (RetiredDatabases.Count > DatabasesToKeep && RetiredDatabases.TryDequeue(out var oldest))
        {
            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            // "with (force)" closes any connection still open to it.
            await using var command = new NpgsqlCommand($"drop database if exists \"{oldest}\" with (force)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }

    private const int DatabasesToKeep = 16;
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> RetiredDatabases = new();

    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DATABASE_ADMIN_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATABASE_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString());
        builder.UseSetting("CORS_ALLOWED_ORIGINS", "*");
        builder.UseSetting("COOKIE_SECURE", "false");
        if (cooldownSeconds is not null) builder.UseSetting("HOLD_COOLDOWN_SECONDS", cooldownSeconds.Value.ToString());
        if (rateLimits) builder.UseSetting("RATE_LIMITS_ENABLED", "true");
        if (signInRequired) builder.UseSetting("SIGN_IN_REQUIRED", "true");
        if (demoMode) builder.UseSetting("DEMO_MODE", "true");
        if (workerSeconds is not null) builder.UseSetting("SEASON_WORKER_SECONDS", workerSeconds.Value.ToString());
        if (adminEmail is not null) builder.UseSetting("ADMIN_EMAIL", adminEmail);
        if (adminPassword is not null) builder.UseSetting("ADMIN_PASSWORD", adminPassword);
        builder.ConfigureTestServices(services => services.AddSingleton<ISmsSender>(Sms));
    }

    // Every text message the app sends, so a test can read the sign-in code.
    public CapturingSmsSender Sms { get; } = new();

    // A client that signs in by itself: a request with a valid X-Student-Id goes out as
    // that student's guardian, any other request as a school office admin. The seat
    // tests stay about seats; the sign-in rules have their own tests.
    public HttpClient CreateSignedInClient() => CreateDefaultClient(new SignInHandler(ConnectionString));

    // A client with no session at all.
    public HttpClient CreateAnonymousClient() => CreateClient();

    private static string CreateFreshDatabase()
    {
        // In docker-compose.tests.yml this points at the throwaway PostgreSQL service.
        var adminConnectionString = AdminConnectionString;

        var databaseName = "admission_test_" + Guid.NewGuid().ToString("N");
        using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            admin.Open();
            using var command = new NpgsqlCommand($"create database \"{databaseName}\"", admin);
            command.ExecuteNonQuery();
        }

        return new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
            MaxPoolSize = 40,
        }.ConnectionString;
    }
}
