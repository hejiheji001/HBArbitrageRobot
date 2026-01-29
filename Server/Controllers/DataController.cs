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
    public async Task<ActionResult<List<LofArbitrageData>>> GetData(CancellationToken cancellationToken)
    {
        var currentShanghaiTime = DataFetchService.GetShanghaiTime();
        
        // Check if cache is valid for current date
        if (_dataStore.IsCacheValidForDate(currentShanghaiTime))
        {
            var cachedData = _dataStore.GetCurrentLofData();
            var cacheDateTime = _dataStore.GetCacheDateTime();
            
            _logger.LogInformation("Returning cached data from {CacheDateTime} (same day as {CurrentTime})", 
                cacheDateTime, currentShanghaiTime);
            
            if (cachedData == null || cachedData.Count == 0)
            {
                return Ok(new List<LofArbitrageData>());
            }
            
            return Ok(cachedData);
        }
        
        // Cache is not valid (different day or no cache), fetch new data
        _logger.LogInformation("Cache invalid or from different day. Fetching new data...");
        
        try
        {
            await _dataFetchService.TriggerManualFetchAsync(cancellationToken);
            var freshData = _dataStore.GetCurrentLofData();
            
            if (freshData == null || freshData.Count == 0)
            {
                return Ok(new List<LofArbitrageData>());
            }
            
            return Ok(freshData);
        }
        catch (InvalidOperationException ex)
        {
            // Fetch already in progress, return current cached data if available
            _logger.LogWarning("Fetch already in progress: {Message}. Returning current cached data.", ex.Message);
            var currentData = _dataStore.GetCurrentLofData();
            
            if (currentData == null || currentData.Count == 0)
            {
                return NotFound(new { message = "Data fetch in progress. Please try again in a moment." });
            }
            
            return Ok(currentData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch fresh data");
            
            // Try to return cached data even if it's old
            var fallbackData = _dataStore.GetCurrentLofData();
            if (fallbackData != null && fallbackData.Count > 0)
            {
                _logger.LogWarning("Returning stale cached data due to fetch failure");
                return Ok(fallbackData);
            }
            
            return NotFound(new { message = "No data available and fetch failed. Data is fetched daily at 14:00 Shanghai time." });
        }
    }

    [HttpPost("fetch")]
    public async Task<IActionResult> TriggerFetch(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual fetch triggered");
        try
        {
            await _dataFetchService.TriggerManualFetchAsync(cancellationToken);
            
            // Return the latest fetched data
            var latestData = _dataStore.GetCurrentLofData();
            var cacheDateTime = _dataStore.GetCacheDateTime();
            
            return Ok(new 
            { 
                message = "Data fetch completed successfully",
                fetchTime = cacheDateTime,
                dataCount = latestData?.Count ?? 0,
                data = latestData ?? new List<LofArbitrageData>()
            });
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
