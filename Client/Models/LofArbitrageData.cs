using System.Text.Json.Serialization;

namespace Client.Models;

public class LofArbitrageDataResponse
{
    public DateTime FetchTime { get; set; }
    public int DataCount { get; set; }
    public List<LofArbitrageData> Data { get; set; } = [];
}

public class LofArbitrageData
{
    public string LofFund { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ApplyStatus { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PurchaseDate { get; set; } = string.Empty;
}

public class LofArbitragePayload
{
    [JsonPropertyName("lofFund")]
    public string LofFund { get; set; } = string.Empty;

    [JsonPropertyName("lofArbitrage")]
    public required LofArbitrage LofArbitrage { get; set; }

    [JsonPropertyName("lofArbitrageStrategy")]
    public required LofArbitrageStrategy LofArbitrageStrategy { get; set; }
}


public class LofArbitrage
{
    [JsonPropertyName("purchaseDate")]
    public required string PurchaseDate { get; set; }

    [JsonPropertyName("purchaseQuantity")]
    public required string PurchaseQuantity { get; set; }
}

public class LofArbitrageStrategy
{
    public int policyType = 4;
    public string sellStockPrice = "";
    public string sellTime = "";
    public string turnBack = "";
}