using System.Reflection;
using Npgsql;

namespace SchoolAdmission.Api.Data;

public static class DatabaseSchemaInitializer
{
    // Any fixed number works. Two backends starting at the same moment wait for each
    // other on this lock, so they never run the schema at the same time.
    private const long SchemaLockKey = 20261111;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);

    public static async Task RunAsync(NpgsqlDataSource dataSource, ILogger logger)
    {
        var schemaSql = ReadSchemaFile();
        var startedAt = DateTime.UtcNow;
        Exception? lastError = null;

        while (DateTime.UtcNow - startedAt < MaxWait)
        {
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();

                await using (var lockCommand = new NpgsqlCommand("select pg_advisory_xact_lock(@key)", connection, transaction))
                {
                    lockCommand.Parameters.AddWithValue("key", SchemaLockKey);
                    await lockCommand.ExecuteNonQueryAsync();
                }

                await using (var schemaCommand = new NpgsqlCommand(schemaSql, connection, transaction))
                {
                    await schemaCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                logger.LogInformation("Database schema is ready.");
                return;
            }
            catch (Exception error) when (error is NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
            {
                lastError = error;
                logger.LogWarning("Database not ready yet ({Message}). Trying again in 2 seconds.", error.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new InvalidOperationException("Could not reach the database within 60 seconds.", lastError);
    }

    private static string ReadSchemaFile()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("admission_schema.sql")
            ?? throw new InvalidOperationException("admission_schema.sql is missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
