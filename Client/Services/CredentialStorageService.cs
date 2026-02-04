using Client.Models;
using SQLite;
using System;
using System.Linq;
using System.Text;

namespace Client.Services;

/// <summary>
/// Service for securely storing and retrieving user credentials
/// </summary>
public class CredentialStorageService
{
    private readonly string _dbPath;
    private readonly byte[] _entropy;

    public event EventHandler<ExecutionResult>? ExecutionResultSaved;

    public CredentialStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "HBArbitrageRobot");
        Directory.CreateDirectory(appFolder);
        
        _dbPath = Path.Combine(appFolder, "credentials.db");
        
        // Use machine-specific entropy for encryption
        _entropy = Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName);
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var db = new SQLiteConnection(_dbPath);
        db.CreateTable<UserCredential>();
        db.CreateTable<ExecutionResult>();
        EnsureExecutionResultSchema(db);
    }

    private static void EnsureExecutionResultSchema(SQLiteConnection db)
    {
        const string tableName = "ExecutionResults";
        var columns = db.GetTableInfo(tableName);
        if (!columns.Any(c => c.Name.Equals(nameof(ExecutionResult.FundSummary), StringComparison.OrdinalIgnoreCase)))
        {
            db.Execute($"ALTER TABLE {tableName} ADD COLUMN {nameof(ExecutionResult.FundSummary)} TEXT");
        }
    }

    /// <summary>
    /// Encrypt sensitive data using DPAPI on Windows or fallback encryption
    /// </summary>
    private string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            // Try DPAPI on Windows
            if (OperatingSystem.IsWindows())
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = System.Security.Cryptography.ProtectedData.Protect(
                    plainBytes, _entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            else
            {
                // Simple XOR encryption for non-Windows (better than plaintext)
                // In production, consider using a proper cross-platform encryption library
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encrypted = new byte[plainBytes.Length];
                for (int i = 0; i < plainBytes.Length; i++)
                {
                    encrypted[i] = (byte)(plainBytes[i] ^ _entropy[i % _entropy.Length]);
                }
                return Convert.ToBase64String(encrypted);
            }
        }
        catch
        {
            // Fallback to base64 if encryption fails
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }
    }

    /// <summary>
    /// Decrypt sensitive data
    /// </summary>
    private string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                    encryptedBytes, _entropy, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var decrypted = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decrypted[i] = (byte)(encryptedBytes[i] ^ _entropy[i % _entropy.Length]);
                }
                return Encoding.UTF8.GetString(decrypted);
            }
        }
        catch
        {
            // Fallback to base64 decode
            return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedText));
        }
    }

    public List<UserCredential> GetAllUsers()
    {
        using var db = new SQLiteConnection(_dbPath);
        var users = db.Table<UserCredential>().ToList();
        
        // Decrypt passwords for use
        foreach (var user in users)
        {
            user.DecryptedPassword = Decrypt(user.EncryptedPassword);
        }
        
        return users;
    }

    public UserCredential? GetUser(int id)
    {
        using var db = new SQLiteConnection(_dbPath);
        var user = db.Table<UserCredential>().FirstOrDefault(u => u.Id == id);
        
        if (user != null)
        {
            user.DecryptedPassword = Decrypt(user.EncryptedPassword);
        }
        
        return user;
    }

    public void SaveUser(UserCredential user)
    {
        using var db = new SQLiteConnection(_dbPath);
        
        // Encrypt passwords before saving
        if (!string.IsNullOrEmpty(user.DecryptedPassword))
        {
            user.EncryptedPassword = Encrypt(user.DecryptedPassword);
        }
        
        if (user.Id == 0)
        {
            db.Insert(user);
        }
        else
        {
            db.Update(user);
        }
    }

    public void DeleteUser(int id)
    {
        using var db = new SQLiteConnection(_dbPath);
        db.Delete<UserCredential>(id);
        
        // Also delete related execution results
        db.Execute("DELETE FROM ExecutionResults WHERE UserCredentialId = ?", id);
    }

    public void SaveExecutionResult(ExecutionResult result)
    {
        using var db = new SQLiteConnection(_dbPath);
        db.Insert(result);
        
        // Update last executed time for user
        var user = db.Table<UserCredential>().FirstOrDefault(u => u.Id == result.UserCredentialId);
        if (user != null)
        {
            user.LastExecutedAt = result.ExecutionTime;
            db.Update(user);
        }

        ExecutionResultSaved?.Invoke(this, result);
    }

    public List<ExecutionResult> GetExecutionResults(int? userCredentialId = null, int limit = 100)
    {
        using var db = new SQLiteConnection(_dbPath);
        
        var query = db.Table<ExecutionResult>();
        
        if (userCredentialId.HasValue)
        {
            query = query.Where(r => r.UserCredentialId == userCredentialId.Value);
        }
        
        return query.OrderByDescending(r => r.ExecutionTime).Take(limit).ToList();
    }
}
