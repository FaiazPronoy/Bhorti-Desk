using System.Data;
using Dapper;

namespace SchoolAdmission.Api.Services;

// Writes a notice for the student of a hold. Filled in fully with the notices feature;
// payment and review already call it so no event is missed.
public class Notices
{
    public const string Held = "HELD";
    public const string TwoMinutesLeft = "TWO_MINUTES_LEFT";
    public const string Paid = "PAID";
    public const string Verified = "VERIFIED";
    public const string Rejected = "REJECTED";
    public const string Expired = "EXPIRED";
    public const string Offered = "OFFERED";

    public Task AddAsync(IDbConnection connection, IDbTransaction? transaction, Guid holdId, string kind) =>
        connection.ExecuteAsync("""
            insert into notices (seat_hold_id, student_birth_registration_number, kind)
            select h.id, h.student_birth_registration_number, @kind from seat_holds h where h.id = @holdId
            on conflict (seat_hold_id, kind) do nothing
            """, new { holdId, kind }, transaction);
}
