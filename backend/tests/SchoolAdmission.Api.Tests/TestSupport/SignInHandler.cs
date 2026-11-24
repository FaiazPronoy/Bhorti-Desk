using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace SchoolAdmission.Api.Tests.TestSupport;

// Adds a real session cookie to each request. Guardians, students and sessions are
// written straight into the test database, the same rows the sign-in flow writes.
public class SignInHandler(string databaseConnectionString) : DelegatingHandler
{
    // No pool: each test has its own database, and idle pooled connections from earlier
    // tests would pile up until PostgreSQL refuses new clients.
    private readonly string connectionString =
        new NpgsqlConnectionStringBuilder(databaseConnectionString) { Pooling = false }.ConnectionString;

    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Sessions = new();
    private static int phoneCounter;
    // At most this many test-setup connections at once, so 300 new students do not open
    // 300 connections in the same moment.
    private static readonly SemaphoreSlim SetupSlots = new(16);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var student = request.Headers.TryGetValues("X-Student-Id", out var values) ? values.First().Trim() : "";
        var isStudent = student.Length == 17 && student.All(char.IsAsciiDigit);
        var key = databaseConnectionString + "|" + (isStudent ? student : "staff");

        var token = await Sessions.GetOrAdd(key, _ => new Lazy<Task<string>>(async () =>
        {
            await SetupSlots.WaitAsync(cancellationToken);
            try
            {
                return isStudent ? await CreateGuardianSessionAsync(student) : await CreateStaffSessionAsync();
            }
            finally
            {
                SetupSlots.Release();
            }
        })).Value;

        request.Headers.Add("Cookie", $"bhorti_session={token}");
        request.Headers.Add("X-Bhorti-Request", "1");
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> CreateGuardianSessionAsync(string brn)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var guardianId = Guid.NewGuid();
        var phone = "013" + Interlocked.Increment(ref phoneCounter).ToString("D8");
        await Execute(connection, "insert into guardians (id, phone) values (@id, @phone)", ("id", guardianId), ("phone", phone));
        await Execute(connection, """
            insert into students (id, guardian_id, birth_registration_number, full_name, date_of_birth, gender)
            values (@id, @guardian, @brn, 'Test Student', date '2015-01-01', 'OTHER')
            """, ("id", Guid.NewGuid()), ("guardian", guardianId), ("brn", brn));
        return await CreateSessionAsync(connection, "GUARDIAN", guardianId);
    }

    private async Task<string> CreateStaffSessionAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var staffId = Guid.NewGuid();
        await Execute(connection, """
            insert into staff_users (id, email, full_name, password_hash, role)
            values (@id, @email, 'Test Admin', 'none', 'ADMIN')
            """, ("id", staffId), ("email", $"admin-{staffId:N}@test.local"));
        return await CreateSessionAsync(connection, "STAFF", staffId);
    }

    private static async Task<string> CreateSessionAsync(NpgsqlConnection connection, string kind, Guid userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await Execute(connection, """
            insert into user_sessions (token_hash, user_kind, user_id, expires_at)
            values (@hash, @kind, @user, clock_timestamp() + interval '1 day')
            """, ("hash", hash), ("kind", kind), ("user", userId));
        return token;
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
