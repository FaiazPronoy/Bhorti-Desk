// School Admission System. Copyright (c) 2026 Faiaz Abrar Zaman Pronoy. MIT License.

using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// total_seats in SQL maps to TotalSeats in C#.
DefaultTypeMap.MatchNamesWithUnderscores = true;

var settings = AdmissionSettings.Load(builder.Configuration);
builder.Services.AddSingleton(settings);

// Registered with a factory so the container owns it and closes its connections when
// the host stops. Each test host would otherwise leave a full pool open.
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(settings.ConnectionString));
builder.Services.AddSingleton<AdmissionDatabase>();
builder.Services.AddScoped<AdmissionClassService>();
builder.Services.AddScoped<SeatHoldService>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 16 * 1024;
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (settings.AllowsAnyOrigin)
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(settings.CorsAllowedOrigins);
    }
    // No cookies are used anywhere, so allowing any origin does not expose a session.
    policy.WithMethods("GET", "POST", "DELETE")
          .WithHeaders("Content-Type", StudentIdHeader.HeaderName)
          .WithExposedHeaders("Retry-After");
}));

builder.Services
    .AddControllers(options => options.Filters.Add<GlobalExceptionHandler>())
    .ConfigureApiBehaviorOptions(options =>
    {
        // Our own error shape for bad JSON, wrong types and a missing body.
        options.InvalidModelStateResponseFactory = context =>
        {
            var fieldErrors = new Dictionary<string, string>();
            foreach (var entry in context.ModelState.Where(entry => entry.Value is { Errors.Count: > 0 }))
            {
                fieldErrors.TryAdd(CleanFieldName(entry.Key), "This value is not valid.");
            }
            return new BadRequestObjectResult(new ApiErrorResponse(ApiErrorCode.InvalidRequest,
                "Some information is not correct.", fieldErrors));
        };
        // Without this, 404 and 415 results get the framework's own body.
        options.SuppressMapClientErrors = true;
    });

var app = builder.Build();

foreach (var warning in settings.Warnings)
{
    app.Logger.LogWarning("{Warning}", warning);
}

// The tables exist before port 8080 answers anything.
var dataSource = app.Services.GetRequiredService<NpgsqlDataSource>();
await DatabaseSchemaInitializer.RunAsync(dataSource, app.Logger);

// CORS first, so every response carries CORS headers, errors included. Otherwise the
// browser reports a plain "class full" answer as a network error.
app.UseCors();

// Last resort for anything that escapes the MVC filter.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ApiErrorResponse(ApiErrorCode.InternalError,
        "Something went wrong on the server."));
}));

// Unknown routes, wrong methods and wrong content types arrive with no body.
// Give them the same error shape as everything else.
app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    var error = response.StatusCode switch
    {
        StatusCodes.Status404NotFound => new ApiErrorResponse(ApiErrorCode.NotFound, "There is nothing at this address."),
        StatusCodes.Status405MethodNotAllowed => new ApiErrorResponse(ApiErrorCode.MethodNotAllowed, "This address does not accept this method."),
        StatusCodes.Status415UnsupportedMediaType => new ApiErrorResponse(ApiErrorCode.InvalidRequest, "Send the request body as JSON."),
        StatusCodes.Status413PayloadTooLarge => new ApiErrorResponse(ApiErrorCode.InvalidRequest, "The request body is too large."),
        _ => new ApiErrorResponse(ApiErrorCode.InvalidRequest, "The request could not be handled."),
    };
    await response.WriteAsJsonAsync(error);
});

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});

app.MapControllers();

app.Run();

static string CleanFieldName(string key)
{
    var name = key.StartsWith("$.") ? key[2..] : key;
    return name.Length == 0 || name == "$" || name == "request" ? "body" : name;
}

// Lets the test project start this app with WebApplicationFactory<Program>.
public partial class Program;
