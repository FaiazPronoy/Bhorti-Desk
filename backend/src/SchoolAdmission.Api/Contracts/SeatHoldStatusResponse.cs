using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

// No studentId here on purpose: this endpoint takes no X-Student-Id header, so anyone
// with the hold id can call it.
public record SeatHoldStatusResponse(
    Guid HoldId,
    Guid ClassId,
    string Status,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ExpiresAt,
    string ClassName,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime CreatedAt,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ServerTime);
