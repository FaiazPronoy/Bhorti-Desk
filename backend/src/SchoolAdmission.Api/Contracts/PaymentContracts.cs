using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Contracts;

public record PaymentStartResponse(Guid PaymentId, int AmountTaka, string Gateway, string Status, string HostedPageUrl);

public record SandboxPageResponse(Guid PaymentId, Guid HoldId, string ClassName, string Serial, int AmountTaka, string Status);

public record SandboxOutcomeRequest(string? Outcome);

// What the gateway posts back. The signature over this exact body is in X-Signature.
public record PaymentCallback(Guid PaymentId, string Status, int AmountTaka);

public record PaymentInfo(
    Guid PaymentId,
    string Gateway,
    string Status,
    int AmountTaka,
    [property: JsonConverter(typeof(UtcSecondsDateTimeConverter))] DateTime? FinishedAt);

public record ReviewInfo(string Decision, string? Reason, bool RefundDue);

public record ReviewRequest(string? Reason);

public record OfficeApplicationResponse(
    Guid HoldId,
    string Serial,
    Guid ClassId,
    string ClassName,
    string Status,
    string StudentIdLastFour,
    ApplicantResponse? Applicant,
    DocumentInfo[] Documents,
    PaymentInfo? Payment,
    ReviewInfo? Review);
