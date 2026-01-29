using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController(DataStore dataStore, ILogger<DataController> logger, DataFetchService dataFetchService) : ControllerBase
{
    /// <summary>
    /// Get LOF arbitrage data with optional client type filtering
    /// </summary>
    /// <param name="from">Optional client type. When set to 'bot', applies time-based access control (14:15 Shanghai time)</param>
    [HttpGet]
    public async Task<ActionResult<LofArbitrageResponse>> GetData([FromQuery] string? from, CancellationToken cancellationToken)
    {
        var currentShanghaiTime = DataFetchService.GetShanghaiTime();
        
        // If 'from' parameter is 'bot', apply time-based logic
        if (from?.ToLowerInvariant() == "bot")
        {
            // Check if current time is before 14:15 Shanghai time
            var targetTime = new TimeSpan(14, 15, 0);
            var currentTime = currentShanghaiTime.TimeOfDay;
            
            if (currentTime < targetTime)
            {
                // Return empty result with custom header
                Response.Headers.Append("X-Message", "please wait until 14:15 Shanghai time");
                logger.LogInformation("Request from bot before 14:15 Shanghai time. Current time: {CurrentTime}. Returning empty result.", 
                    currentShanghaiTime);
                
                return Ok(new LofArbitrageResponse
                {
                    FetchTime = currentShanghaiTime,
                    DataCount = 0,
                    Data = []
                });
            }
            
            // After 14:15, attempt to update cache if not from current date
            if (!dataStore.IsCacheValidForDate(currentShanghaiTime))
            {
                logger.LogInformation("Request from bot after 14:15. Cache is not from today. Attempting to fetch fresh data...");
                
                try
                {
                    await dataFetchService.TriggerManualFetchAsync(cancellationToken);
                    
                    // Verify the cache was updated successfully
                    if (!dataStore.IsCacheValidForDate(currentShanghaiTime))
                    {
                        logger.LogWarning("Cache update did not result in current date data. May still be fetching or failed.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    logger.LogWarning("Fetch already in progress: {Message}. Will return current cache if available.", ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fetch fresh data for bot request");
                }
            }
            
            // Return the current cache data
            var botData = dataStore.GetCurrentLofData();
            var botFetchTime = dataStore.GetCacheDateTime();
            
            // If no data available, return 404 similar to non-bot behavior
            if (botData == null || botData.Count == 0)
            {
                if (botFetchTime == null)
                {
                    return NotFound(new { message = "No data available. Data is fetched daily at 14:00 Shanghai time." });
                }
            }
            
            logger.LogInformation("Returning data to bot. FetchTime: {FetchTime}, DataCount: {Count}", 
                botFetchTime, botData?.Count ?? 0);
            
            return Ok(new LofArbitrageResponse
            {
                FetchTime = botFetchTime ?? currentShanghaiTime,
                DataCount = botData?.Count ?? 0,
                Data = botData ?? []
            });
        }
        
        // Original logic for non-bot requests
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
