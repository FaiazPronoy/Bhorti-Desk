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
}
