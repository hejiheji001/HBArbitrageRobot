namespace Client.Models;

public class ArbitrageData
{
    public string? Content { get; set; }
    public DateTime FetchedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}
