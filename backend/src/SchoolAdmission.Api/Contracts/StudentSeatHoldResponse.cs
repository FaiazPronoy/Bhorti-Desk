using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

public record StudentSeatHoldResponse(
    Guid HoldId,
    Guid ClassId,
    string ClassName,
    string Status,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ExpiresAt,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime CreatedAt,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ServerTime);
