using Dapper;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
public class HealthController(AdmissionDatabase database) : ControllerBase
{
    // Not part of the assignment. Answers 200 only when the database answers too.
    [HttpGet("health")]
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

    // Ready to take traffic: the database answers and the newest tables exist, so a load
    // balancer does not send requests to a process that is still setting up.
    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync();
            var ready = await connection.ExecuteScalarAsync<bool>(
                "select to_regclass('public.waitlist_entries') is not null and to_regclass('public.notices') is not null");
            return ready ? Ok(new { status = "ready" }) : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "schema not ready" });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "database unreachable" });
        }
    }
}
