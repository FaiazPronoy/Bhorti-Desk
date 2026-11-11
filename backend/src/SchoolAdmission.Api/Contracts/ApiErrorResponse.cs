using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

public record ApiErrorResponse(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string>? FieldErrors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? HoldId = null);
