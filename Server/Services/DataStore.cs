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
            return _currentData;
        }
    }
}
