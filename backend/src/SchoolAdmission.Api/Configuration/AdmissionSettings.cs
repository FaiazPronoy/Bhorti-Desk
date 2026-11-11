namespace SchoolAdmission.Api.Configuration;

public class AdmissionSettings
{
    public const int DefaultHoldDurationSeconds = 600;

    public required string ConnectionString { get; init; }
    public required int HoldDurationSeconds { get; init; }
    public required string[] CorsAllowedOrigins { get; init; }

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
        };
        settings.Warnings.AddRange(warnings);
        return settings;
    }
}
