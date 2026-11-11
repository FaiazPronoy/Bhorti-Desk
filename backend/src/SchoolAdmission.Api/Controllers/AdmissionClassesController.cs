using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Errors;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class AdmissionClassesController(AdmissionClassService classService, SeatHoldService holdService) : ControllerBase
{
    private const int MaxNameLength = 100;
    private const int MaxSeats = 100_000;

    // 1. POST /classes
    [HttpPost]
    public async Task<ActionResult<AdmissionClassResponse>> Create([FromBody] CreateAdmissionClassRequest? request)
    {
        // A body of just "null" (or nothing) reaches here as null. Treat it as an empty form.
        request ??= new CreateAdmissionClassRequest(null, null);
        var fieldErrors = new Dictionary<string, string>();
        var name = request.Name?.Trim() ?? "";

        if (name.Length == 0)
        {
            fieldErrors["name"] = "Enter a class name.";
        }
        else if (name.Length > MaxNameLength)
        {
            fieldErrors["name"] = $"The class name can be at most {MaxNameLength} characters.";
        }
        else if (name.Any(char.IsControl))
        {
            fieldErrors["name"] = "The class name has characters that are not allowed.";
        }

        if (request.TotalSeats is null)
        {
            fieldErrors["totalSeats"] = "Enter the number of seats.";
        }
        else if (request.TotalSeats < 1 || request.TotalSeats > MaxSeats)
        {
            fieldErrors["totalSeats"] = $"The number of seats must be from 1 to {MaxSeats}.";
        }

        if (fieldErrors.Count > 0)
        {
            return BadRequest(new ApiErrorResponse(ApiErrorCode.InvalidRequest,
                "Some information is not correct.", fieldErrors));
        }

        var created = await classService.CreateAsync(name, request.TotalSeats!.Value);
        return Created($"/classes/{created.Id}", created);
    }

    // 2. GET /classes
    [HttpGet]
    public async Task<List<AdmissionClassSeatSummaryResponse>> List()
    {
        return await classService.ListAsync();
    }

    // 3. GET /classes/{classId}
    // The id comes in as a string and is parsed here. With a Guid parameter the framework
    // would answer a bad id itself, with its own body. Ours is simply 404.
    [HttpGet("{classId}")]
    public async Task<AdmissionClassSeatSummaryResponse> Get(string classId)
    {
        return await classService.GetAsync(ParseClassId(classId));
    }

    // 4. POST /classes/{classId}/holds, with the X-Student-Id header. No request body.
    [HttpPost("{classId}/holds")]
    public async Task<ActionResult<SeatHoldCreatedResponse>> CreateHold(string classId)
    {
        var studentId = StudentIdHeader.Read(Request);
        var created = await holdService.CreateHoldAsync(ParseClassId(classId), studentId);
        return Created($"/holds/{created.HoldId}", created);
    }

    private static Guid ParseClassId(string classId) =>
        Guid.TryParse(classId, out var id) ? id : throw AdmissionRuleException.ClassNotFound();
}
