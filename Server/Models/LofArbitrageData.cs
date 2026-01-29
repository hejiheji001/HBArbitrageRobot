namespace Server.Models;

public class LofArbitrageData
{
    public string LofFund { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ApplyStatus { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PurchaseDate { get; set; } = string.Empty;
}
