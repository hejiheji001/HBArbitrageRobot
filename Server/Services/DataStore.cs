using Microsoft.Extensions.Caching.Memory;
using Server.Models;

namespace Server.Services;

public class DataStore(IMemoryCache cache)
{
    private const string DATA_CACHE_KEY = "LofArbitrageData";
    private const string DATETIME_CACHE_KEY = "LofArbitrageFetchDateTime";

    public void UpdateLofData(List<LofArbitrageData> data, DateTime fetchDateTime)
    {
        // Create a defensive copy when storing
        var dataCopy = data.Select(d => new LofArbitrageData
        {
            LofFund = d.LofFund,
            Name = d.Name,
            ApplyStatus = d.ApplyStatus,
            Amount = d.Amount,
            PurchaseDate = d.PurchaseDate
        }).ToList();

        // Store data in cache with no expiration (manual management)
        cache.Set(DATA_CACHE_KEY, dataCopy, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
        
        cache.Set(DATETIME_CACHE_KEY, fetchDateTime, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }

    public List<LofArbitrageData>? GetCurrentLofData()
    {
        // Get data from cache and return a defensive copy
        if (!cache.TryGetValue<List<LofArbitrageData>>(DATA_CACHE_KEY, out var cachedData) || cachedData == null)
            return null;

        return cachedData.Select(d => new LofArbitrageData
        {
            LofFund = d.LofFund,
            Name = d.Name,
            ApplyStatus = d.ApplyStatus,
            Amount = d.Amount,
            PurchaseDate = d.PurchaseDate
        }).ToList();
    }

    public DateTime? GetCacheDateTime()
    {
        return cache.TryGetValue<DateTime>(DATETIME_CACHE_KEY, out var cacheDateTime) 
            ? cacheDateTime 
            : null;
    }

    public bool IsCacheValidForDate(DateTime currentDate)
    {
        if (!cache.TryGetValue<DateTime>(DATETIME_CACHE_KEY, out var cacheDateTime))
            return false;

        if (!cache.TryGetValue<List<LofArbitrageData>>(DATA_CACHE_KEY, out var cachedData) || cachedData == null)
            return false;

        // Check if cache is from the same day (year, month, day)
        return cacheDateTime.Year == currentDate.Year &&
               cacheDateTime.Month == currentDate.Month &&
               cacheDateTime.Day == currentDate.Day;
    }
}
