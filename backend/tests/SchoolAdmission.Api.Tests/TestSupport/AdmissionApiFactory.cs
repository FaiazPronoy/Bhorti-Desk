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

    public string ConnectionString { get; }

    // Pass a connection string to put a second API process on the same database.
    public AdmissionApiFactory(int holdSeconds = 600, string? connectionString = null,
        string? adminEmail = null, string? adminPassword = null)
    {
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
        if (!ownsDatabase) return;

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        // "with (force)" closes any connection still open to it.
        await using var command = new NpgsqlCommand($"drop database if exists \"{databaseName}\" with (force)", admin);
        await command.ExecuteNonQueryAsync();
        GC.SuppressFinalize(this);
    }

    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DATABASE_ADMIN_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATABASE_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString());
        builder.UseSetting("CORS_ALLOWED_ORIGINS", "*");
        builder.UseSetting("COOKIE_SECURE", "false");
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
