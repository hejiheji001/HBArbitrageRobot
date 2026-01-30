using Newtonsoft.Json;

namespace Client.Models;

public class CreateArbitrageRequest
{
    [JsonProperty("stockHolderList")]
    public List<string> StockHolderList { get; set; } = [];

    [JsonProperty("purchaseTime")]
    public string PurchaseTime { get; set; } = string.Empty;

    [JsonProperty("list")]
    public List<LofArbitragePayload> List { get; set; } = [];

    [JsonProperty("pwd")]
    public string Password { get; set; } = string.Empty;
}
