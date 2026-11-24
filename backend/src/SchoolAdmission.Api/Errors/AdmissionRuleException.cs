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

    public static AdmissionRuleException SignInRequired() =>
        new(StatusCodes.Status401Unauthorized, ApiErrorCode.SignInRequired, "Sign in first.");

    public static AdmissionRuleException NotAllowed() =>
        new(StatusCodes.Status403Forbidden, ApiErrorCode.NotAllowed, "Your account cannot do this.");

    public static AdmissionRuleException CsrfRequired() =>
        new(StatusCodes.Status403Forbidden, ApiErrorCode.CsrfRequired, "This request did not come from the Bhorti Desk app.");

    public static AdmissionRuleException PhoneInvalid() =>
        new(StatusCodes.Status400BadRequest, ApiErrorCode.PhoneInvalid, "Enter an 11 digit Bangladeshi mobile number, like 01712345678.");

    public static AdmissionRuleException OtpWrong() =>
        new(StatusCodes.Status400BadRequest, ApiErrorCode.OtpWrong, "The code is wrong or has expired. Ask for a new one.");

    public static AdmissionRuleException OtpTooSoon() =>
        new(StatusCodes.Status429TooManyRequests, ApiErrorCode.OtpTooSoon, "A code was sent just now. Wait a minute before asking again.");

    public static AdmissionRuleException OtpTooMany() =>
        new(StatusCodes.Status429TooManyRequests, ApiErrorCode.OtpTooMany, "Too many codes for this number. Try again in an hour.");

    public static AdmissionRuleException LoginFailed() =>
        new(StatusCodes.Status401Unauthorized, ApiErrorCode.LoginFailed, "The email or password is wrong.");

    public static AdmissionRuleException StudentNotYours() =>
        new(StatusCodes.Status403Forbidden, ApiErrorCode.StudentNotYours, "This student is not on your account. Add the student first.");

    public static AdmissionRuleException StudentTaken() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.StudentTaken,
            "This birth registration number is already on another account. Contact the school office if this is your child.");

    public static AdmissionRuleException TooManyHolds(int limit) =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.TooManyHolds,
            $"Your family already has {limit} unpaid seats on hold. Pay for one or cancel one first.");

    public static AdmissionRuleException Cooldown(int seconds) =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.Cooldown,
            $"Your last hold in this class ran out without payment. You can apply again in {Math.Max(1, (seconds + 59) / 60)} minutes.")
        { RetryAfterSeconds = seconds };

    public static AdmissionRuleException SessionNotFound() =>
        new(StatusCodes.Status404NotFound, ApiErrorCode.SessionNotFound, "No admission session has this id.");

    public static AdmissionRuleException SessionNotOpen() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.SessionNotOpen, "Admission for this class is not open right now.");

    public static AdmissionRuleException SessionAlreadyPublished() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.SessionAlreadyPublished, "Another session is already published. Close it first.");

    public static AdmissionRuleException DocumentNotFound() =>
        new(StatusCodes.Status404NotFound, ApiErrorCode.DocumentNotFound, "No such document for this application.");

    public static AdmissionRuleException PaymentNotFound() =>
        new(StatusCodes.Status404NotFound, ApiErrorCode.PaymentNotFound, "No payment has this id.");

    public static AdmissionRuleException PaymentKeyReused() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.PaymentKeyReused, "This Idempotency-Key was already used for another application.");

    public static AdmissionRuleException AlreadyPaid() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.HoldAlreadyConfirmed, "This admission is already paid.");

    public static AdmissionRuleException BadSignature() =>
        new(StatusCodes.Status400BadRequest, ApiErrorCode.BadSignature, "The payment callback could not be verified.");

    public static AdmissionRuleException AlreadyReviewed() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.AlreadyReviewed, "This application already has a decision.");

    public static AdmissionRuleException NotPaidYet() =>
        new(StatusCodes.Status409Conflict, ApiErrorCode.NotPaidYet, "Only a paid application can be reviewed.");

    public int? RetryAfterSeconds { get; init; }
}
