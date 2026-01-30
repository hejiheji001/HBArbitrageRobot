using System.Text.Json.Serialization;

namespace Client.Models;

public class CreateArbitrageRequest
{
    [JsonPropertyName("stockHolderList")]
    public List<string> StockHolderList { get; set; } = [];

    [JsonPropertyName("purchaseTime")]
    public string PurchaseTime { get; set; } = string.Empty;

    [JsonPropertyName("list")]
    public List<LofArbitragePayload> List { get; set; } = [];

    [JsonPropertyName("pwd")]
    public string Password { get; set; } = string.Empty;
}
