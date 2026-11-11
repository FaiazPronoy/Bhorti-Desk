using Dapper;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Services;

public class AdmissionClassService(AdmissionDatabase database, ILogger<AdmissionClassService> logger)
{
    // Seat numbers are never stored. They are counted from seat_holds every time, so
    // there is no counter that could drift. A HELD row whose time has passed is not counted.
    private const string SeatSummarySql = """
        select c.id, c.name, c.total_seats,
               count(h.id) filter (where h.status = 'HELD' and h.expires_at > clock_timestamp()) as held_seats,
               count(h.id) filter (where h.status = 'CONFIRMED')                                 as confirmed_seats
        from admission_classes c
        left join seat_holds h
               on h.admission_class_id = c.id and h.status in ('HELD', 'CONFIRMED')
        """;

    public async Task<AdmissionClassResponse> CreateAsync(string name, int totalSeats)
    {
        var id = Guid.CreateVersion7();
        await using var connection = await database.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "insert into admission_classes (id, name, total_seats) values (@id, @name, @totalSeats)",
            new { id, name, totalSeats });

        logger.LogInformation("Class created: {ClassId} with {TotalSeats} seats", id, totalSeats);
        return new AdmissionClassResponse(id, name, totalSeats);
    }

    public async Task<List<AdmissionClassSeatSummaryResponse>> ListAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var rows = await connection.QueryAsync<ClassSeatRow>(
            SeatSummarySql + " group by c.id order by c.created_at, c.id");
        return rows.Select(ToResponse).ToList();
    }

    public async Task<AdmissionClassSeatSummaryResponse> GetAsync(Guid classId)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<ClassSeatRow>(
            SeatSummarySql + " where c.id = @classId group by c.id", new { classId });
        return row is null ? throw AdmissionRuleException.ClassNotFound() : ToResponse(row);
    }

    private static AdmissionClassSeatSummaryResponse ToResponse(ClassSeatRow row)
    {
        var held = (int)row.HeldSeats;
        var confirmed = (int)row.ConfirmedSeats;
        return new AdmissionClassSeatSummaryResponse(
            row.Id, row.Name, row.TotalSeats, held, confirmed, row.TotalSeats - held - confirmed);
    }

    private class ClassSeatRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int TotalSeats { get; set; }
        public long HeldSeats { get; set; }
        public long ConfirmedSeats { get; set; }
    }
}
