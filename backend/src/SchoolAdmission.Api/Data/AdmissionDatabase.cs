using Npgsql;

namespace SchoolAdmission.Api.Data;

// One shared pool of connections for the whole app.
public class AdmissionDatabase(NpgsqlDataSource dataSource)
{
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await dataSource.OpenConnectionAsync(cancellationToken);
    }
}
