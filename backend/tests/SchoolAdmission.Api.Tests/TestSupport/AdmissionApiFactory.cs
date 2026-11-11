using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace SchoolAdmission.Api.Tests.TestSupport;

// Starts the real API in memory against a real PostgreSQL database. Locks cannot be
// tested against a fake database, so every factory gets a brand new database of its own.
public class AdmissionApiFactory : WebApplicationFactory<Program>
{
    private readonly int holdSeconds;

    public string ConnectionString { get; }

    // Pass a connection string to put a second API process on the same database.
    public AdmissionApiFactory(int holdSeconds = 600, string? connectionString = null)
    {
        this.holdSeconds = holdSeconds;
        ConnectionString = connectionString ?? CreateFreshDatabase();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DATABASE_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString());
        builder.UseSetting("CORS_ALLOWED_ORIGINS", "*");
    }

    private static string CreateFreshDatabase()
    {
        // In docker-compose.tests.yml this points at the throwaway PostgreSQL service.
        var adminConnectionString = Environment.GetEnvironmentVariable("TEST_DATABASE_ADMIN_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

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
