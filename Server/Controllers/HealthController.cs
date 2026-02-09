using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Health check endpoint for AWS Lambda Web Adapter and monitoring
    /// </summary>
    [HttpGet("/health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Root endpoint
    /// </summary>
    [HttpGet("/")]
    public IActionResult GetRoot()
    {
        return Ok(new { message = "HBArbitrageRobot Server API", status = "running" });
    }
}
