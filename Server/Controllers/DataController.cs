using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly DataStore _dataStore;
    private readonly ILogger<DataController> _logger;
    private readonly DataFetchService _dataFetchService;

    public DataController(DataStore dataStore, ILogger<DataController> logger, DataFetchService dataFetchService)
    {
        _dataStore = dataStore;
        _logger = logger;
        _dataFetchService = dataFetchService;
    }

    [HttpGet]
    public ActionResult<List<LofArbitrageData>> GetData()
    {
        var data = _dataStore.GetCurrentLofData();

        if (data == null || data.Count == 0)
        {
            _logger.LogWarning("No data available yet");
            return NotFound(new { message = "No data available yet. Data is fetched daily at 14:00 Shanghai time." });
        }

        return Ok(data);
    }

    [HttpPost("fetch")]
    public async Task<IActionResult> TriggerFetch(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual fetch triggered");
        try
        {
            await _dataFetchService.TriggerManualFetchAsync(cancellationToken);
            return Ok(new { message = "Data fetch completed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Manual fetch rejected: {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual fetch failed");
            return StatusCode(500, new { message = "Data fetch failed. Please try again later." });
        }
    }
}
