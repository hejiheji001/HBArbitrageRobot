using Client.Models;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Text;

namespace Client.Services;

/// <summary>
/// Service for executing arbitrage for multiple users sequentially
/// </summary>
public class MultiUserExecutionService
{
    private readonly ApiClientService _apiClient;
    private readonly CredentialStorageService _credentialStorage;
    private readonly string _apiUrl;
    private readonly string _purchaseTime;
    private readonly decimal _maxInvestment;

    public event EventHandler<ExecutionProgressEventArgs>? ProgressUpdated;
    public event EventHandler<ExecutionResult>? UserExecutionCompleted;

    public MultiUserExecutionService(
        ApiClientService apiClient,
        CredentialStorageService credentialStorage,
        string apiUrl,
        string purchaseTime,
        decimal maxInvestment)
    {
        _apiClient = apiClient;
        _credentialStorage = credentialStorage;
        _apiUrl = apiUrl;
        _purchaseTime = purchaseTime;
        _maxInvestment = maxInvestment;
    }

    /// <summary>
    /// Execute arbitrage for all enabled users sequentially
    /// </summary>
    public async Task<List<ExecutionResult>> ExecuteForAllUsersAsync()
    {
        var users = _credentialStorage.GetAllUsers()
            .Where(u => u.IsEnabled)
            .ToList();

        if (users.Count == 0)
        {
            ReportProgress("No enabled users found");
            return new List<ExecutionResult>();
        }

        ReportProgress($"Starting execution for {users.Count} user(s)");

        var results = new List<ExecutionResult>();

        for (int i = 0; i < users.Count; i++)
        {
            var user = users[i];
            ReportProgress($"Processing user {i + 1}/{users.Count}: {user.DisplayName ?? user.Username}");

            try
            {
                var result = await ExecuteForUserAsync(user);
                results.Add(result);
                UserExecutionCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                var errorResult = new ExecutionResult
                {
                    UserCredentialId = user.Id,
                    Username = user.Username,
                    Success = false,
                    ErrorMessage = ex.Message,
                    LogMessages = $"Fatal error: {ex.Message}\n{ex.StackTrace}"
                };
                
                results.Add(errorResult);
                _credentialStorage.SaveExecutionResult(errorResult);
                UserExecutionCompleted?.Invoke(this, errorResult);
                
                ReportProgress($"Error processing user {user.Username}: {ex.Message}");
            }

            // Small delay between users
            if (i < users.Count - 1)
            {
                await Task.Delay(2000);
            }
        }

        ReportProgress($"Completed execution for all users. Success: {results.Count(r => r.Success)}/{results.Count}");
        return results;
    }

    /// <summary>
    /// Execute arbitrage for a single user
    /// </summary>
    private async Task<ExecutionResult> ExecuteForUserAsync(UserCredential user)
    {
        var sw = Stopwatch.StartNew();
        var logMessages = new StringBuilder();
        var result = new ExecutionResult
        {
            UserCredentialId = user.Id,
            Username = user.Username,
            ExecutionTime = DateTime.Now
        };

        void Log(string message)
        {
            logMessages.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            ReportProgress($"[{user.Username}] {message}");
        }

        try
        {
            Log("Fetching LOF data from API...");
            var (lofData, shouldWait) = await _apiClient.FetchLofDataFromApiAsync(_apiUrl);

            if (shouldWait || lofData == null || lofData.Count == 0)
            {
                Log("No arbitrage opportunities available");
                result.Success = true; // Not an error, just no data
                result.LogMessages = logMessages.ToString();
                result.DurationSeconds = sw.Elapsed.TotalSeconds;
                _credentialStorage.SaveExecutionResult(result);
                return result;
            }

            Log($"Fetched {lofData.Count} LOF items");
            result.LofItemsProcessed = lofData.Count;

            // Initialize browser and execute workflow
            var browserService = new BrowserService();
            var authService = new AuthenticationService(user.DecryptedPassword ?? "", user.DecryptedPassword ?? "");
            var arbitrageService = new ArbitrageService(authService);

            Log("Initializing browser (headless mode)...");
            var (playwright, browser) = await browserService.InitializeBrowserAsync(headless: true);

            using (playwright)
            {
                await using (browser)
                {
                    var context = await browser.NewContextAsync(playwright.Devices["iPhone 13 Pro"]);
                    var page = await context.NewPageAsync();

                    // Login
                    Log("Logging in to touker.com...");
                    var loginUrl = "https://m.touker.com/account/login/index.htm?loginType=normal";
                    await page.GotoAsync(loginUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await authService.LogInAsync(page);
                    Log("Login successful");

                    // Navigate to main page
                    Log("Navigating to arbitrage page...");
                    var mainUrl = "https://m.touker.com/fd/lofrobot/sceneConstruction";
                    await browserService.NavigateWithRetryAsync(page, mainUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    // Fetch stock holders
                    Log("Fetching stock holder list...");
                    var stockHolderList = await arbitrageService.FetchStockHolderListAsync(page);
                    Log($"Found {stockHolderList.Count} stock holders");
                    result.StockHoldersCount = stockHolderList.Count;

                    if (stockHolderList.Count == 0)
                    {
                        throw new InvalidOperationException("No stock holders found");
                    }

                    // Calculate investments
                    arbitrageService.CalculateInvestments(lofData, stockHolderList, _maxInvestment);
                    var totalInvestment = lofData.Sum(item => item.Amount * stockHolderList.Count);
                    result.TotalInvestment = totalInvestment;
                    Log($"Total investment calculated: {totalInvestment:N2} yuan");

                    // Create arbitrage orders
                    Log("Creating arbitrage orders...");
                    var lofPayload = lofData.Select(item => new LofArbitragePayload
                    {
                        LofFund = item.LofFund,
                        LofArbitrageStrategy = new(),
                        LofArbitrage = new LofArbitrage
                        {
                            PurchaseDate = item.PurchaseDate,
                            PurchaseQuantity = item.Amount.ToString()
                        }
                    }).ToList();

                    var request = new CreateArbitrageRequest
                    {
                        StockHolderList = stockHolderList,
                        PurchaseTime = _purchaseTime,
                        List = lofPayload,
                        Password = user.DecryptedTransactionPassword ?? user.DecryptedPassword ?? ""
                    };

                    var success = await arbitrageService.CreateArbitrageAsync(page, request);

                    if (success)
                    {
                        Log("✅ Arbitrage created successfully!");
                        result.Success = true;
                    }
                    else
                    {
                        Log("❌ Failed to create arbitrage");
                        result.Success = false;
                        result.ErrorMessage = "Failed to create arbitrage";
                    }

                    await Task.Delay(2000);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            logMessages.AppendLine($"\nStack trace:\n{ex.StackTrace}");
        }

        sw.Stop();
        result.DurationSeconds = sw.Elapsed.TotalSeconds;
        result.LogMessages = logMessages.ToString();
        
        _credentialStorage.SaveExecutionResult(result);
        
        return result;
    }

    private void ReportProgress(string message)
    {
        ProgressUpdated?.Invoke(this, new ExecutionProgressEventArgs { Message = message });
    }
}

public class ExecutionProgressEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
}
