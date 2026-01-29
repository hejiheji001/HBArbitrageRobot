using Microsoft.Extensions.Caching.Memory;
using Server.Models;

namespace Server.Services;

/// <summary>
/// Stores LOF arbitrage data with associated fetch timestamp
/// </summary>
public record CachedLofData(List<LofArbitrageData> Data, DateTime FetchDateTime);

public class DataStore(IMemoryCache cache)
{
    private const string CacheKey = "LofArbitrageDataWithTimestamp";

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

        // Store both data and timestamp atomically in a single cache entry
        var cachedData = new CachedLofData(dataCopy, fetchDateTime);
        cache.Set(CacheKey, cachedData, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }

    public List<LofArbitrageData>? GetCurrentLofData()
    {
        // Get data from cache and return a defensive copy
        if (!cache.TryGetValue<CachedLofData>(CacheKey, out var cachedData) || cachedData == null)
            return null;

        return cachedData.Data.Select(d => new LofArbitrageData
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
        return cache.TryGetValue<CachedLofData>(CacheKey, out var cachedData) && cachedData != null
            ? cachedData.FetchDateTime
            : null;
    }

    public bool IsCacheValidForDate(DateTime currentDate)
    {
        if (!cache.TryGetValue<CachedLofData>(CacheKey, out var cachedData) || cachedData == null)
            return false;

        var cacheDateTime = cachedData.FetchDateTime;
        
        // Check if cache is from the same day (year, month, day)
        return cacheDateTime.Year == currentDate.Year &&
               cacheDateTime.Month == currentDate.Month &&
               cacheDateTime.Day == currentDate.Day;
    }
}
