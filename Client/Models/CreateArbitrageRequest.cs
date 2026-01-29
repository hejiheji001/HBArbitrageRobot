namespace Client.Models;

public class CreateArbitrageRequest
{
    public List<string> StockHolderList { get; set; } = [];
    public string PurchaseTime { get; set; } = string.Empty;
    public List<LofArbitrageData> List { get; set; } = [];
    public string Pwd { get; set; } = string.Empty;
}
