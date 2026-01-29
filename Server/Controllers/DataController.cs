using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController(DataStore dataStore, ILogger<DataController> logger, DataFetchService dataFetchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LofArbitrageData>>> GetData(CancellationToken cancellationToken)
    {
        var currentShanghaiTime = DataFetchService.GetShanghaiTime();
        
        // Check if cache is valid for current date
        if (dataStore.IsCacheValidForDate(currentShanghaiTime))
        {
            var cachedData = dataStore.GetCurrentLofData();
            var cacheDateTime = dataStore.GetCacheDateTime();
            
            logger.LogInformation("Returning cached data from {CacheDateTime} (same day as {CurrentTime})", 
                cacheDateTime, currentShanghaiTime);
            
            if (cachedData == null || cachedData.Count == 0)
            {
                return Ok(new List<LofArbitrageData>());
            }
            
            return Ok(cachedData);
        }
        
        // Cache is not valid (different day or no cache), fetch new data
        logger.LogInformation("Cache invalid or from different day. Fetching new data...");
        
        try
        {
            await dataFetchService.TriggerManualFetchAsync(cancellationToken);
            var freshData = dataStore.GetCurrentLofData();
            
            if (freshData == null || freshData.Count == 0)
            {
                return Ok(new List<LofArbitrageData>());
            }
            
            return Ok(freshData);
        }
        catch (InvalidOperationException ex)
        {
            // Fetch already in progress, return current cached data if available
            logger.LogWarning("Fetch already in progress: {Message}. Returning current cached data.", ex.Message);
            var currentData = dataStore.GetCurrentLofData();
            
            if (currentData == null || currentData.Count == 0)
            {
                return NotFound(new { message = "Data fetch in progress. Please try again in a moment." });
            }
            
            return Ok(currentData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch fresh data");
            
            // Try to return cached data even if it's old
            var fallbackData = dataStore.GetCurrentLofData();
            if (fallbackData != null && fallbackData.Count > 0)
            {
                logger.LogWarning("Returning stale cached data due to fetch failure");
                return Ok(fallbackData);
            }
            
            return NotFound(new { message = "No data available and fetch failed. Data is fetched daily at 14:00 Shanghai time." });
        }
    }
}
