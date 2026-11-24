using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Auth;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
public class PaymentsController(PaymentService payments, StudentService students) : ControllerBase
{
    [HttpPost("applications/{holdId}/payments")]
    public async Task<PaymentStartResponse> Start(string holdId)
    {
        var studentId = StudentIdHeader.Read(Request);
        await StudentAccess.RequireOwnAsync(HttpContext, students, studentId);
        return await payments.StartAsync(ParseId(holdId, AdmissionRuleException.HoldNotFound), studentId, Request.Headers["Idempotency-Key"].ToString());
    }

    // The sandbox gateway's own hosted page and buttons.
    [HttpGet("sandbox-gateway/payments/{paymentId}")]
    public Task<SandboxPageResponse> SandboxPage(string paymentId) =>
        payments.SandboxPageAsync(ParseId(paymentId, AdmissionRuleException.PaymentNotFound));

    [HttpPost("sandbox-gateway/payments/{paymentId}/decide")]
    public async Task<object> SandboxDecide(string paymentId, [FromBody] SandboxOutcomeRequest? request) =>
        new { status = await payments.SandboxDecideAsync(ParseId(paymentId, AdmissionRuleException.PaymentNotFound), request?.Outcome) };

    // Where a gateway posts the signed result. The raw body is read as is, because the
    // signature is over those exact bytes.
    [HttpPost("payments/callback")]
    public async Task<object> Callback()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        return new { status = await payments.HandleCallbackAsync(body, Request.Headers["X-Signature"].ToString()) };
    }

    private static Guid ParseId(string id, Func<AdmissionRuleException> notFound) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw notFound();
}

[ApiController]
[Route("office/applications")]
public class OfficeApplicationsController(ReviewService reviews) : ControllerBase
{
    [HttpGet]
    public Task<List<OfficeApplicationResponse>> List()
    {
        Access.Staff(HttpContext);
        return reviews.ListAsync();
    }

    [HttpPost("{holdId}/verify")]
    public Task<OfficeApplicationResponse> Verify(string holdId) =>
        reviews.VerifyAsync(Access.Staff(HttpContext), ParseHoldId(holdId));

    [HttpPost("{holdId}/reject")]
    public Task<OfficeApplicationResponse> Reject(string holdId, [FromBody] ReviewRequest? request) =>
        reviews.RejectAsync(Access.Staff(HttpContext), ParseHoldId(holdId), request?.Reason);

    private static Guid ParseHoldId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed : throw AdmissionRuleException.HoldNotFound();
}
