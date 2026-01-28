using Server.Models;

namespace Server.Services;

public class DataStore
{
    private ArbitrageData? _currentData;
    private readonly object _lock = new();

    public void UpdateData(ArbitrageData data)
    {
        lock (_lock)
        {
            _currentData = data;
        }
    }

    public ArbitrageData? GetCurrentData()
    {
        lock (_lock)
        {
            // Return a copy to prevent external modifications
            if (_currentData == null) return null;
            
            return new ArbitrageData
            {
                Content = _currentData.Content,
                FetchedAt = _currentData.FetchedAt,
                Source = _currentData.Source
            };
        }
    }
}
