namespace Client.Models;

public class CreateArbitrageRequest
{
    public List<string> StockHolderList { get; set; } = new();
    public string PurchaseTime { get; set; } = string.Empty;
    public List<LofArbitrageData> List { get; set; } = new();
    public string Pwd { get; set; } = string.Empty;
}
