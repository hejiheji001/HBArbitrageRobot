using Server.Models;

namespace Server.Services;

public class DataStore
{
    private List<LofArbitrageData>? _currentLofData;
    private DateTime? _cacheDateTime;
    private readonly Lock _lock = new();

    public void UpdateLofData(List<LofArbitrageData> data, DateTime fetchDateTime)
    {
        lock (_lock)
        {
            // Create a defensive copy when storing
            _currentLofData = [.. data.Select(d => new LofArbitrageData
            {
                LofFund = d.LofFund,
                Name = d.Name,
                ApplyStatus = d.ApplyStatus,
                Amount = d.Amount,
                PurchaseDate = d.PurchaseDate
            })];
            
            _cacheDateTime = fetchDateTime;
        }
    }

    public List<LofArbitrageData>? GetCurrentLofData()
    {
        lock (_lock)
        {
            // Return a copy to prevent external modifications
            if (_currentLofData == null) return null;
            
            return [.. _currentLofData.Select(d => new LofArbitrageData
            {
                LofFund = d.LofFund,
                Name = d.Name,
                ApplyStatus = d.ApplyStatus,
                Amount = d.Amount,
                PurchaseDate = d.PurchaseDate
            })];
        }
    }

    public DateTime? GetCacheDateTime()
    {
        lock (_lock)
        {
            return _cacheDateTime;
        }
    }

    public bool IsCacheValidForDate(DateTime currentDate)
    {
        lock (_lock)
        {
            if (_cacheDateTime == null || _currentLofData == null)
                return false;

            // Check if cache is from the same day (year, month, day)
            return _cacheDateTime.Value.Year == currentDate.Year &&
                   _cacheDateTime.Value.Month == currentDate.Month &&
                   _cacheDateTime.Value.Day == currentDate.Day;
        }
    }
}
