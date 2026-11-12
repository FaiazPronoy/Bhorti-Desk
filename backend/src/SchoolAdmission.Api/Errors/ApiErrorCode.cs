namespace SchoolAdmission.Api.Errors;

// The "code" field of every error body. The frontend shows a message for each one.
public static class ApiErrorCode
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string StudentIdRequired = "STUDENT_ID_REQUIRED";
    public const string StudentIdInvalid = "STUDENT_ID_INVALID";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string ClassFull = "CLASS_FULL";
    public const string AlreadyApplied = "ALREADY_APPLIED";
    public const string HoldNotOwned = "HOLD_NOT_OWNED";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldAlreadyConfirmed = "HOLD_ALREADY_CONFIRMED";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string ServerBusy = "SERVER_BUSY";
    public const string InternalError = "INTERNAL_ERROR";

    // Accounts and sign-in.
    public const string SignInRequired = "SIGN_IN_REQUIRED";
    public const string NotAllowed = "NOT_ALLOWED";
    public const string CsrfRequired = "CSRF_REQUIRED";
    public const string PhoneInvalid = "PHONE_INVALID";
    public const string OtpWrong = "OTP_WRONG";
    public const string OtpTooSoon = "OTP_TOO_SOON";
    public const string OtpTooMany = "OTP_TOO_MANY";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string StudentNotYours = "STUDENT_NOT_YOURS";
    public const string StudentTaken = "STUDENT_TAKEN";
    public const string RateLimited = "RATE_LIMITED";
    public const string TooManyHolds = "TOO_MANY_HOLDS";
    public const string Cooldown = "APPLY_COOLDOWN";
}
