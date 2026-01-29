namespace Client.Models;

public class LofArbitrageData
{
    public string LofFund { get; set; } = string.Empty;
    public string ApplyStatus { get; set; } = string.Empty;
    public LofArbitrage LofArbitrage { get; set; } = new();
    public LofArbitrageStrategy LofArbitrageStrategy { get; set; } = new();
}

public class LofArbitrage
{
    public string PurchaseQuantity { get; set; } = string.Empty;
    public string PurchaseDate { get; set; } = string.Empty;
}

public class LofArbitrageStrategy
{
    public int PolicyType { get; set; }
    public string SellTime { get; set; } = string.Empty;
    public string SellStockPrice { get; set; } = string.Empty;
    public string TurnBack { get; set; } = string.Empty;
}
