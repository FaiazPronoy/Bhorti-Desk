using Dapper;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Security;

namespace SchoolAdmission.Api.Services;

public class AuthService(AdmissionDatabase database, AdmissionSettings settings, ISmsSender sms, ILogger<AuthService> logger)
{
    private const int OtpLifetimeMinutes = 5;
    private const int OtpMaxAttempts = 5;
    private const int OtpCooldownSeconds = 60;
    private const int OtpMaxPerHour = 5;

    // Compared against when an email is unknown, so a wrong email takes as long as a
    // wrong password and nobody can find out which staff emails exist.
    private static readonly string DummyHash = PasswordHasher.Hash(Guid.NewGuid().ToString());

    // ---------------------------------------------------------------- guardians

    public async Task RequestOtpAsync(string? rawPhone)
    {
        var phone = PhoneNumber.Normalize(rawPhone) ?? throw AdmissionRuleException.PhoneInvalid();
        await using var connection = await database.OpenConnectionAsync();

        var recent = await connection.QuerySingleAsync<OtpCountRow>("""
            select count(*) filter (where created_at > clock_timestamp() - interval '1 hour') as last_hour,
                   count(*) filter (where created_at > clock_timestamp() - make_interval(secs => @cooldown)) as last_minute
              from otp_codes where phone = @phone
            """, new { phone, cooldown = OtpCooldownSeconds });
        if (recent.LastMinute > 0) throw AdmissionRuleException.OtpTooSoon();
        if (recent.LastHour >= OtpMaxPerHour) throw AdmissionRuleException.OtpTooMany();

        var code = Tokens.NewOtpCode();
        await connection.ExecuteAsync("""
            insert into otp_codes (id, phone, code_hash, expires_at)
            values (@id, @phone, @hash, clock_timestamp() + make_interval(mins => @minutes))
            """, new { id = Guid.CreateVersion7(), phone, hash = Tokens.Sha256Hex(phone + ":" + code), minutes = OtpLifetimeMinutes });

        await sms.SendAsync(phone, $"Bhorti Desk code: {code}. It works for {OtpLifetimeMinutes} minutes. Do not share it.");
        if (settings.LogOtpCodes)
        {
            logger.LogWarning("Sign-in code for {Phone}: {Code} (OTP_LOG_CODES is on)", phone, code);
        }
    }

    public async Task<string> VerifyOtpAsync(string? rawPhone, string? code)
    {
        var phone = PhoneNumber.Normalize(rawPhone) ?? throw AdmissionRuleException.PhoneInvalid();
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6) throw AdmissionRuleException.OtpWrong();

        await using var connection = await database.OpenConnectionAsync();
        var latest = await connection.QuerySingleOrDefaultAsync<OtpRow>("""
            select id, code_hash, attempts from otp_codes
             where phone = @phone and consumed_at is null and expires_at > clock_timestamp()
             order by created_at desc limit 1
            """, new { phone });
        if (latest is null || latest.Attempts >= OtpMaxAttempts) throw AdmissionRuleException.OtpWrong();

        var expected = Convert.FromHexString(latest.CodeHash);
        var actual = Convert.FromHexString(Tokens.Sha256Hex(phone + ":" + code.Trim()));
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            await connection.ExecuteAsync("update otp_codes set attempts = attempts + 1 where id = @id", new { id = latest.Id });
            throw AdmissionRuleException.OtpWrong();
        }

        // Use the code exactly once, even if two tabs send it at the same moment.
        var consumed = await connection.ExecuteAsync(
            "update otp_codes set consumed_at = clock_timestamp() where id = @id and consumed_at is null", new { id = latest.Id });
        if (consumed == 0) throw AdmissionRuleException.OtpWrong();

        var guardianId = await connection.QuerySingleAsync<Guid>("""
            insert into guardians (id, phone) values (@id, @phone)
            on conflict (phone) do update set phone = excluded.phone
            returning id
            """, new { id = Guid.CreateVersion7(), phone });

        logger.LogInformation("Guardian signed in: {Phone}", PhoneNumber.Masked(phone));
        return await CreateSessionAsync(connection, "GUARDIAN", guardianId);
    }

    // ---------------------------------------------------------------- staff

    public async Task<string> StaffLoginAsync(string? email, string? password)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? "";
        await using var connection = await database.OpenConnectionAsync();
        var staff = await connection.QuerySingleOrDefaultAsync<StaffRow>(
            "select id, password_hash, disabled from staff_users where email = @email", new { email = normalized });

        var passwordOk = PasswordHasher.Verify(password ?? "", staff?.PasswordHash ?? DummyHash);
        if (staff is null || staff.Disabled || !passwordOk)
        {
            logger.LogInformation("Staff sign-in failed for an email ending {Domain}", normalized.Split('@').LastOrDefault());
            throw AdmissionRuleException.LoginFailed();
        }
        return await CreateSessionAsync(connection, "STAFF", staff.Id);
    }

    public async Task EnsureFirstAdminAsync()
    {
        await using var connection = await database.OpenConnectionAsync();
        var staffCount = await connection.ExecuteScalarAsync<long>("select count(*) from staff_users");
        if (staffCount > 0) return;

        if (string.IsNullOrWhiteSpace(settings.AdminEmail) || string.IsNullOrEmpty(settings.AdminPassword))
        {
            logger.LogWarning("No school office account exists. Set ADMIN_EMAIL and ADMIN_PASSWORD to create the first one.");
            return;
        }
        if (settings.AdminPassword.Length < 12)
        {
            logger.LogError("ADMIN_PASSWORD must be at least 12 characters. The first admin was not created.");
            return;
        }
        await connection.ExecuteAsync("""
            insert into staff_users (id, email, full_name, password_hash, role)
            values (@id, @email, 'School admin', @hash, 'ADMIN')
            on conflict (email) do nothing
            """, new { id = Guid.CreateVersion7(), email = settings.AdminEmail.ToLowerInvariant(), hash = PasswordHasher.Hash(settings.AdminPassword) });
        logger.LogInformation("Created the first school office admin account.");
    }

    // ---------------------------------------------------------------- sessions

    public async Task<CurrentUser?> FindSessionAsync(string tokenHash)
    {
        await using var connection = await database.OpenConnectionAsync();
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>("""
            select s.user_kind, s.user_id, st.role, coalesce(st.disabled, false) as disabled
              from user_sessions s
              left join staff_users st on s.user_kind = 'STAFF' and st.id = s.user_id
             where s.token_hash = @tokenHash and s.revoked_at is null and s.expires_at > clock_timestamp()
            """, new { tokenHash });
        if (row is null || row.Disabled) return null;
        return new CurrentUser(row.UserKind == "STAFF" ? UserKind.Staff : UserKind.Guardian, row.UserId, row.Role);
    }

    public async Task SignOutAsync(string tokenHash)
    {
        await using var connection = await database.OpenConnectionAsync();
        await connection.ExecuteAsync(
            "update user_sessions set revoked_at = clock_timestamp() where token_hash = @tokenHash and revoked_at is null", new { tokenHash });
    }

    private static async Task<string> CreateSessionAsync(Npgsql.NpgsqlConnection connection, string kind, Guid userId)
    {
        var token = Tokens.NewSessionToken();
        await connection.ExecuteAsync("""
            insert into user_sessions (token_hash, user_kind, user_id, expires_at)
            values (@hash, @kind, @userId, clock_timestamp() + make_interval(days => @days))
            """, new { hash = Tokens.Sha256Hex(token), kind, userId, days = (int)SessionCookie.Lifetime.TotalDays });
        return token;
    }

    private class OtpCountRow { public long LastHour { get; set; } public long LastMinute { get; set; } }
    private class OtpRow { public Guid Id { get; set; } public string CodeHash { get; set; } = ""; public int Attempts { get; set; } }
    private class StaffRow { public Guid Id { get; set; } public string PasswordHash { get; set; } = ""; public bool Disabled { get; set; } }
    private class SessionRow { public string UserKind { get; set; } = ""; public Guid UserId { get; set; } public string? Role { get; set; } public bool Disabled { get; set; } }
}
