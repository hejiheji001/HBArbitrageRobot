namespace Server.Models;

/// <summary>
/// DTO for API response containing LOF arbitrage data
/// </summary>
public class LofArbitrageResponse
{
    public DateTime FetchTime { get; set; }
    public int DataCount { get; set; }
    public List<LofArbitrageData> Data { get; set; } = [];
}
