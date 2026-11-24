using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

public record SessionRequest(int? AcademicYear, string? Name, DateTimeOffset? OpensAt, DateTimeOffset? ClosesAt);

public record AttachClassRequest(Guid? ClassId);

public record SessionResponse(
    Guid Id,
    int AcademicYear,
    string Name,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime OpensAt,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ClosesAt,
    string Status,
    // UPCOMING, OPEN or ENDED, worked out from the database clock.
    string Window,
    Guid[] ClassIds);

public record CurrentSeasonResponse(
    SessionResponse? Session,
    [property: JsonConverter(typeof(UtcMillisecondsDateTimeConverter))] DateTime ServerTime);
