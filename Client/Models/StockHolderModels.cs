namespace Client.Models;

public class StockHolderResponse
{
    public List<StockHolder>? Result { get; set; }
    public int Code { get; set; }
    public bool Success { get; set; }
}

public class StockHolder
{
    public string StockHolderNo { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
}
