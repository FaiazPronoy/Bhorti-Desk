namespace SchoolAdmission.Api.Configuration;

public class AdmissionSettings
{
    public const int DefaultHoldDurationSeconds = 600;

    public required string ConnectionString { get; init; }
    public required int HoldDurationSeconds { get; init; }
    public required string[] CorsAllowedOrigins { get; init; }

    // Off by default, so the eight original endpoints answer exactly as the contract says:
    // no login, identity from X-Student-Id. Turn it on for a real deployment.
    public bool SignInRequired { get; init; }

    // Anti-abuse limits. All off by default for the same reason.
    public int MaxActiveHoldsPerGuardian { get; init; } = 3;
    public int HoldCooldownSeconds { get; init; }
    public bool RateLimitsEnabled { get; init; }

    // First admin account, created at start when there is no staff yet.
    public string? AdminEmail { get; init; }
    public string? AdminPassword { get; init; }

    // Without an SMS provider the one-time code is written to the log. Only for local use.
    public bool LogOtpCodes { get; init; }

    // Shared secret for signing sandbox payment callbacks and slip codes.
    public string PaymentSandboxSecret { get; init; } = DefaultLocalSecret;
    public string SlipSecret { get; init; } = DefaultLocalSecret;
    public const string DefaultLocalSecret = "local-only-secret-change-me";

    // Off only for plain http on localhost. Behind HTTPS it must be on.
    public bool SecureCookies { get; init; } = true;

    // Problems found while reading the settings. Logged once the app has a logger.
    public List<string> Warnings { get; } = new();

    public bool AllowsAnyOrigin => CorsAllowedOrigins.Length == 0 || CorsAllowedOrigins.Contains("*");

    public static AdmissionSettings Load(IConfiguration configuration)
    {
        var connectionString = configuration["DATABASE_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["ConnectionStrings:Default"];
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No database connection string. Set DATABASE_CONNECTION_STRING or ConnectionStrings__Default.");
        }

        var warnings = new List<string>();
        var holdText = configuration["HOLD_DURATION_SECONDS"];
        var holdSeconds = DefaultHoldDurationSeconds;
        if (!string.IsNullOrWhiteSpace(holdText))
        {
            if (int.TryParse(holdText, out var parsed) && parsed > 0)
            {
                holdSeconds = parsed;
            }
            else
            {
                warnings.Add($"HOLD_DURATION_SECONDS is \"{holdText}\", which is not a positive whole number. Using {DefaultHoldDurationSeconds}.");
            }
        }

        var origins = (configuration["CORS_ALLOWED_ORIGINS"] ?? "*")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var settings = new AdmissionSettings
        {
            ConnectionString = connectionString,
            HoldDurationSeconds = holdSeconds,
            CorsAllowedOrigins = origins,
            SignInRequired = IsTrue(configuration["SIGN_IN_REQUIRED"]),
            MaxActiveHoldsPerGuardian = PositiveOr(configuration["MAX_ACTIVE_HOLDS_PER_GUARDIAN"], 3, allowZero: false),
            HoldCooldownSeconds = PositiveOr(configuration["HOLD_COOLDOWN_SECONDS"], 0, allowZero: true),
            RateLimitsEnabled = IsTrue(configuration["RATE_LIMITS_ENABLED"]),
            AdminEmail = configuration["ADMIN_EMAIL"]?.Trim(),
            AdminPassword = configuration["ADMIN_PASSWORD"],
            LogOtpCodes = IsTrue(configuration["OTP_LOG_CODES"]),
            SecureCookies = !IsFalse(configuration["COOKIE_SECURE"]),
            PaymentSandboxSecret = NonEmptyOr(configuration["PAYMENT_SANDBOX_SECRET"], DefaultLocalSecret),
            SlipSecret = NonEmptyOr(configuration["SLIP_SECRET"], DefaultLocalSecret),
        };
        if (settings.PaymentSandboxSecret == DefaultLocalSecret || settings.SlipSecret == DefaultLocalSecret)
        {
            settings.Warnings.Add("PAYMENT_SANDBOX_SECRET or SLIP_SECRET is the local default. Set both before anyone else can reach the app.");
        }
        settings.Warnings.AddRange(warnings);
        if (settings.LogOtpCodes)
        {
            settings.Warnings.Add("OTP_LOG_CODES is on: sign-in codes are written to the log. Never use this in production.");
        }
        return settings;
    }

    private static int PositiveOr(string? value, int fallback, bool allowZero) =>
        int.TryParse(value, out var parsed) && (parsed > 0 || (allowZero && parsed == 0)) ? parsed : fallback;

    private static string NonEmptyOr(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static bool IsTrue(string? value) => value?.Trim().ToLowerInvariant() is "true" or "1" or "yes";
    private static bool IsFalse(string? value) => value?.Trim().ToLowerInvariant() is "false" or "0" or "no";
}
