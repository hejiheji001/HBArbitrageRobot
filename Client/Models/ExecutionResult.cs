using SQLite;

namespace Client.Models;

/// <summary>
/// Represents the result of an arbitrage execution for a user
/// </summary>
[Table("ExecutionResults")]
public class ExecutionResult
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserCredentialId { get; set; }

    public string Username { get; set; } = string.Empty;

    public DateTime ExecutionTime { get; set; } = DateTime.Now;

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public int LofItemsProcessed { get; set; }

    public int StockHoldersCount { get; set; }

    public decimal TotalInvestment { get; set; }

    /// <summary>
    /// Summary of fund allocations captured during execution
    /// </summary>
    public string FundSummary { get; set; } = string.Empty;

    /// <summary>
    /// Detailed log messages from the execution
    /// </summary>
    public string LogMessages { get; set; } = string.Empty;

    /// <summary>
    /// Duration of execution in seconds
    /// </summary>
    public double DurationSeconds { get; set; }
}
