using System.Data;
using Dapper;
using SchoolAdmission.Api.Auth;

namespace SchoolAdmission.Api.Services;

// One line for every office action. It is written on the same connection and transaction
// as the change, so a change can never be saved without its record.
public static class AuditLog
{
    public static Task WriteAsync(IDbConnection connection, IDbTransaction? transaction, CurrentUser staff,
        string action, string targetType, string targetId, string? detail = null) =>
        connection.ExecuteAsync("""
            insert into audit_log (actor_id, actor_role, action, target_type, target_id, detail)
            values (@actorId, @actorRole, @action, @targetType, @targetId, @detail)
            """,
            new { actorId = staff.Id, actorRole = staff.Role ?? "", action, targetType, targetId, detail },
            transaction);
}
