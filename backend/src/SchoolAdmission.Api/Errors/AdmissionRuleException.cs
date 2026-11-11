namespace SchoolAdmission.Api.Errors;

// Thrown when a request breaks one of the admission rules. GlobalExceptionHandler turns
// it into a JSON error with the right status code. It is not a crash.
public class AdmissionRuleException(int statusCode, string code, string message, Guid? holdId = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public Guid? HoldId { get; } = holdId;

    public static AdmissionRuleException ClassNotFound() =>
        new(StatusCodes.Status404NotFound, ApiErrorCode.ClassNotFound, "No class has this id.");

    public static AdmissionRuleException HoldNotFound() =>
        new(StatusCodes.Status404NotFound, ApiErrorCode.HoldNotFound, "No application has this id.");

    public static AdmissionRuleException ClassFull() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.ClassFull, "No seats are left in this class.");

    public static AdmissionRuleException AlreadyApplied(Guid? existingHoldId) =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.AlreadyApplied,
            "This student already has an application in this class.", existingHoldId);

    public static AdmissionRuleException NotOwned() =>
        new(StatusCodes.Status403Forbidden, ApiErrorCode.HoldNotOwned,
            "This application belongs to a different student.");

    public static AdmissionRuleException Expired() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.HoldExpired, "The time for this application is over.");

    public static AdmissionRuleException Cancelled() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.HoldCancelled, "This application was cancelled.");

    public static AdmissionRuleException AlreadyConfirmed() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.HoldAlreadyConfirmed,
            "This admission is already confirmed and cannot be cancelled.");

    public static AdmissionRuleException ServerBusy() =>
        new(StatusCodes.Status503ServiceUnavailable, ApiErrorCode.ServerBusy,
            "Too many requests are waiting for this class. Try again in a moment.");
}
