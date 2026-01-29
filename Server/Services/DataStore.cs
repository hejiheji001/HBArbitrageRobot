using Server.Models;

namespace Server.Services;

public class DataStore
{
    private List<LofArbitrageData>? _currentLofData;
    private readonly object _lock = new();

    public void UpdateLofData(List<LofArbitrageData> data)
    {
        lock (_lock)
        {
            // Create a defensive copy when storing
            _currentLofData = data.Select(d => new LofArbitrageData
            {
                LofFund = d.LofFund,
                LofArbitrage = new LofArbitrage
                {
                    PurchaseQuantity = d.LofArbitrage.PurchaseQuantity,
                    PurchaseDate = d.LofArbitrage.PurchaseDate
                },
                LofArbitrageStrategy = new LofArbitrageStrategy
                {
                    PolicyType = d.LofArbitrageStrategy.PolicyType,
                    SellTime = d.LofArbitrageStrategy.SellTime,
                    SellStockPrice = d.LofArbitrageStrategy.SellStockPrice,
                    TurnBack = d.LofArbitrageStrategy.TurnBack
                }
            }).ToList();
        }
    }

    public List<LofArbitrageData>? GetCurrentLofData()
    {
        lock (_lock)
        {
            // Return a copy to prevent external modifications
            if (_currentLofData == null) return null;
            
            return _currentLofData.Select(d => new LofArbitrageData
            {
                LofFund = d.LofFund,
                LofArbitrage = new LofArbitrage
                {
                    PurchaseQuantity = d.LofArbitrage.PurchaseQuantity,
                    PurchaseDate = d.LofArbitrage.PurchaseDate
                },
                LofArbitrageStrategy = new LofArbitrageStrategy
                {
                    PolicyType = d.LofArbitrageStrategy.PolicyType,
                    SellTime = d.LofArbitrageStrategy.SellTime,
                    SellStockPrice = d.LofArbitrageStrategy.SellStockPrice,
                    TurnBack = d.LofArbitrageStrategy.TurnBack
                }
            }).ToList();
        }
    }
}
