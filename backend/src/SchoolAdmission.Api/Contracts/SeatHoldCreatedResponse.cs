using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

// className and serverTime are extra fields. The UI uses serverTime to run its
// countdown on the database clock instead of the phone's clock.
public record SeatHoldCreatedResponse(
    Guid HoldId,
    Guid ClassId,
    string StudentId,
    string Status,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ExpiresAt,
    string ClassName,
    [property: JsonConverter(typeof(UtcMillisecondsDateTimeConverter))] DateTime ServerTime);
