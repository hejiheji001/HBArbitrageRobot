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

    public DataController(DataStore dataStore, ILogger<DataController> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<ArbitrageData> GetData()
    {
        var data = _dataStore.GetCurrentData();

        if (data == null)
        {
            _logger.LogWarning("No data available yet");
            return NotFound(new { message = "No data available yet. Data is fetched daily at 14:00 Shanghai time." });
        }

        return Ok(data);
    }
}
