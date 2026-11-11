using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;
using SchoolAdmission.Api.Contracts;

namespace SchoolAdmission.Api.Errors;

// Turns exceptions from controllers and services into the JSON error shape.
//
// This is an MVC filter rather than the exception handler middleware on purpose: the
// middleware clears the response, CORS headers included, so a browser would see a
// "class full" answer as a network error. A filter writes a normal response, and the
// CORS headers stay on it.
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case AdmissionRuleException rule:
                logger.LogInformation("{StatusCode} {Code} on {Method} {Path}", rule.StatusCode, rule.Code,
                    context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                if (rule.StatusCode == StatusCodes.Status503ServiceUnavailable)
                {
                    context.HttpContext.Response.Headers.RetryAfter = "2";
                }
                context.Result = new ObjectResult(new ApiErrorResponse(rule.Code, rule.Message, HoldId: rule.HoldId))
                {
                    StatusCode = rule.StatusCode,
                };
                break;

            // Kestrel throws this for a body that is too large or cut off.
            case BadHttpRequestException badRequest:
                logger.LogInformation("{StatusCode} INVALID_REQUEST: {Message}", badRequest.StatusCode, badRequest.Message);
                context.Result = new ObjectResult(new ApiErrorResponse(ApiErrorCode.InvalidRequest,
                    "The request could not be read."))
                {
                    StatusCode = badRequest.StatusCode,
                };
                break;

            // A lock wait that ran out, or a deadlock, anywhere outside the hold retry: the
            // database is busy, not broken. Ask the caller to try again.
            case PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected } busy:
                logger.LogWarning("503 SERVER_BUSY ({SqlState}) on {Method} {Path}", busy.SqlState,
                    context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                context.HttpContext.Response.Headers.RetryAfter = "2";
                context.Result = new ObjectResult(new ApiErrorResponse(ApiErrorCode.ServerBusy,
                    "Many people are applying right now. Please try again in a moment."))
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                };
                break;

            default:
                // A real bug. Log the details, but never send them to the caller.
                logger.LogError(context.Exception, "Unexpected error on {Method} {Path}",
                    context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                context.Result = new ObjectResult(new ApiErrorResponse(ApiErrorCode.InternalError,
                    "Something went wrong on the server."))
                {
                    StatusCode = StatusCodes.Status500InternalServerError,
                };
                break;
        }

        context.ExceptionHandled = true;
    }
}
