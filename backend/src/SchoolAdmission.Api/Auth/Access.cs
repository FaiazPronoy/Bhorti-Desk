using SchoolAdmission.Api.Errors;

namespace SchoolAdmission.Api.Auth;

// The checks a controller calls at the top of an action. Each one throws the right
// 401 or 403 when it fails, so an action never runs for the wrong person.
public static class Access
{
    private const string ItemKey = "bhorti.current-user";

    public static void Set(HttpContext context, CurrentUser user) => context.Items[ItemKey] = user;

    public static CurrentUser? Find(HttpContext context) => context.Items[ItemKey] as CurrentUser;

    public static CurrentUser Guardian(HttpContext context)
    {
        var user = Find(context) ?? throw AdmissionRuleException.SignInRequired();
        return user.IsGuardian ? user : throw AdmissionRuleException.NotAllowed();
    }

    public static CurrentUser Staff(HttpContext context, string? role = null)
    {
        var user = Find(context) ?? throw AdmissionRuleException.SignInRequired();
        if (!user.IsStaff) throw AdmissionRuleException.NotAllowed();
        // ADMIN can do everything an OFFICER can.
        if (role is not null && user.Role != role && user.Role != StaffRole.Admin) throw AdmissionRuleException.NotAllowed();
        return user;
    }
}

public static class StaffRole
{
    public const string Admin = "ADMIN";
    public const string Officer = "OFFICER";
}
