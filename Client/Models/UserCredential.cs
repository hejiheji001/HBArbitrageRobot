using SQLite;

namespace Client.Models;

/// <summary>
/// Represents a user credential for arbitrage execution
/// </summary>
[Table("UserCredentials")]
public class UserCredential
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted password stored in database
    /// </summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? LastExecutedAt { get; set; }

    [Ignore]
    public string? DecryptedPassword { get; set; }
}
