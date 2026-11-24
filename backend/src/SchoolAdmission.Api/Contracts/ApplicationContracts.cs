using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

public record ClassRulesRequest(int? MinAge, int? MaxAge, DateOnly? AgeCutoffDate, int? FeeTaka);

public record ClassRulesResponse(Guid ClassId, int? MinAge, int? MaxAge, DateOnly? AgeCutoffDate, int FeeTaka);

public record ApplicantRequest(
    string? StudentNameBn,
    string? StudentNameEn,
    DateOnly? DateOfBirth,
    string? GuardianName,
    string? GuardianPhone,
    string? PreviousSchool);

public record ApplicantResponse(
    string StudentNameBn,
    string StudentNameEn,
    DateOnly DateOfBirth,
    string GuardianName,
    string GuardianPhone,
    string? PreviousSchool);

// Everything the application page needs beyond the original GET /holds/{id}.
public record ApplicationResponse(
    Guid HoldId,
    string Serial,
    Guid ClassId,
    string ClassName,
    string Status,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime ExpiresAt,
    [property: JsonConverter(typeof(UtcMillisecondsDateTimeConverter))] DateTime ServerTime,
    ClassRulesResponse? Rules,
    ApplicantResponse? Applicant,
    DocumentInfo[] Documents);

public record DocumentInfo(string Kind, string ContentType, int SizeBytes);
