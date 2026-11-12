namespace SchoolAdmission.Api.Auth;

public enum UserKind { Guardian, Staff }

// Who sent this request, found from the session cookie. Role is set for staff only.
public sealed record CurrentUser(UserKind Kind, Guid Id, string? Role)
{
    public bool IsGuardian => Kind == UserKind.Guardian;
    public bool IsStaff => Kind == UserKind.Staff;
}
