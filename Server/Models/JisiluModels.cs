using System.Text.Json.Serialization;

namespace Server.Models;

/// <summary>
/// Represents the root response from Jisilu API
/// </summary>
public class JisiluResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("rows")]
    public List<JisiluRow> Rows { get; set; } = new();
}

/// <summary>
/// Represents a single row in the Jisilu response
/// </summary>
public class JisiluRow
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("cell")]
    public JisiluCell Cell { get; set; } = new();
}

/// <summary>
/// Represents the cell data containing fund information
/// </summary>
public class JisiluCell
{
    [JsonPropertyName("fund_id")]
    public string FundId { get; set; } = string.Empty;

    [JsonPropertyName("fund_nm")]
    public string FundNm { get; set; } = string.Empty;

    [JsonPropertyName("discount_rt")]
    public string DiscountRt { get; set; } = string.Empty;

    [JsonPropertyName("redeem_status")]
    public string RedeemStatus { get; set; } = string.Empty;

    [JsonPropertyName("apply_status")]
    public string ApplyStatus { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("fund_nav")]
    public string FundNav { get; set; } = string.Empty;

    [JsonPropertyName("lof_type")]
    public string LofType { get; set; } = string.Empty;
}
