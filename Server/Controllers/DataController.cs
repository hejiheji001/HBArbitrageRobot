using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController(DataStore dataStore, ILogger<DataController> logger, DataFetchService dataFetchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LofArbitrageResponse>> GetData(CancellationToken cancellationToken)
    {
        var currentShanghaiTime = DataFetchService.GetShanghaiTime();
        
        // Check if cache is valid for current date
        if (dataStore.IsCacheValidForDate(currentShanghaiTime))
        {
            var cachedData = dataStore.GetCurrentLofData();
            var cacheDateTime = dataStore.GetCacheDateTime();
            
            logger.LogInformation("Returning cached data from {CacheDateTime} (same day as {CurrentTime})", 
                cacheDateTime, currentShanghaiTime);
            
            return Ok(new LofArbitrageResponse
            {
                FetchTime = cacheDateTime ?? currentShanghaiTime,
                DataCount = cachedData?.Count ?? 0,
                Data = cachedData ?? []
            });
        }
        
        // Cache is not valid (different day or no cache), fetch new data
        logger.LogInformation("Cache invalid or from different day. Fetching new data...");
        
        try
        {
            await dataFetchService.TriggerManualFetchAsync(cancellationToken);
            var freshData = dataStore.GetCurrentLofData();
            var fetchTime = dataStore.GetCacheDateTime();
            
            return Ok(new LofArbitrageResponse
            {
                FetchTime = fetchTime ?? currentShanghaiTime,
                DataCount = freshData?.Count ?? 0,
                Data = freshData ?? []
            });
        }
        catch (InvalidOperationException ex)
        {
            // Fetch already in progress, return current cached data if available
            logger.LogWarning("Fetch already in progress: {Message}. Returning current cached data.", ex.Message);
            var currentData = dataStore.GetCurrentLofData();
            var currentFetchTime = dataStore.GetCacheDateTime();
            
            if (currentData == null || currentData.Count == 0)
            {
                return NotFound(new { message = "Data fetch in progress. Please try again in a moment." });
            }
            
            return Ok(new LofArbitrageResponse
            {
                FetchTime = currentFetchTime ?? currentShanghaiTime,
                DataCount = currentData.Count,
                Data = currentData
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch fresh data");
            
            // Try to return cached data even if it's old
            var fallbackData = dataStore.GetCurrentLofData();
            var fallbackFetchTime = dataStore.GetCacheDateTime();
            
            if (fallbackData != null && fallbackData.Count > 0)
            {
                logger.LogWarning("Returning stale cached data due to fetch failure");
                return Ok(new LofArbitrageResponse
                {
                    FetchTime = fallbackFetchTime ?? currentShanghaiTime,
                    DataCount = fallbackData.Count,
                    Data = fallbackData
                });
            }
            
            return NotFound(new { message = "No data available and fetch failed. Data is fetched daily at 14:00 Shanghai time." });
        }
    }
}
