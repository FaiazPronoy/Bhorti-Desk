using Dapper;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(AdmissionDatabase database) : ControllerBase
{
    // Not part of the assignment. Answers 200 only when the database answers too.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync();
            await connection.ExecuteScalarAsync<int>("select 1");
            return Ok(new { status = "ok" });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "database unreachable" });
        }
    }
}
