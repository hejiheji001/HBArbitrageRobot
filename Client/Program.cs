using Microsoft.Playwright;
using Newtonsoft.Json;
using Client.Models;

class Program
{
    // Interface for error notification - can be extended to webhook integration
    private interface IErrorNotifier
    {
        Task NotifyAsync(string message, Exception? exception = null);
    }
    
    // Console implementation of error notifier
    private class ConsoleErrorNotifier : IErrorNotifier
    {
        public Task NotifyAsync(string message, Exception? exception = null)
        {
            Console.WriteLine($"\n❌ ERROR: {message}");
            if (exception != null)
            {
                Console.WriteLine($"   Exception: {exception.Message}");
                Console.WriteLine($"   Stack trace: {exception.StackTrace}");
            }
            // TODO: Integrate webhook notification here when needed
            return Task.CompletedTask;
        }
    }
    
    private static readonly IErrorNotifier errorNotifier = new ConsoleErrorNotifier();

    static async Task Main(string[] args)
    {
        Console.WriteLine("HBArbitrageRobot Client Starting...");
        Console.WriteLine("Running in continuous mode - will check for arbitrage opportunities periodically");

        // Parse arguments or environment variables for credentials
        var username = GetConfig(args, "USERNAME", 0);
        var password = GetConfig(args, "PASSWORD", 1);
        var apiUrl = GetConfig(args, "API_URL", 2, "http://localhost:5000");
        var purchaseTime = GetConfig(args, "PURCHASE_TIME", 4, "14:15");
        
        // Configurable max investment per stock holder per fund (in yuan)
        var maxInvestmentPerHolder = decimal.Parse(GetConfig(args, "MAX_INVESTMENT", 5, "1000"));

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("ERROR: Username and password are required.");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Testing: dotnet run <username> <password> [api_url] [pwd] [purchase_time]");
            Console.WriteLine("  Production: Set USERNAME, PASSWORD, PWD, PURCHASE_TIME environment variables");
            return;
        }

        try
        {
            // Main infinite loop
            while (true)
            {
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine($"Starting arbitrage cycle at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine(new string('=', 80));
                
                try
                {
                    await RunArbitrageCycleAsync(username, password, apiUrl, purchaseTime, maxInvestmentPerHolder);
                }
                catch (Exception ex)
                {
                    await errorNotifier.NotifyAsync("Error during arbitrage cycle", ex);
                    Console.WriteLine("Will retry in the next cycle...");
                }
                
                // Enter standby mode and wait until 14:20
                await EnterStandbyModeAsync();
            }
        }
        catch (Exception ex)
        {
            await errorNotifier.NotifyAsync("Fatal error in main loop", ex);
            Environment.Exit(1);
        }
    }
    
    private static async Task RunArbitrageCycleAsync(string username, string password, string apiUrl, 
        string purchaseTime, decimal maxInvestmentPerHolder)
    {
        try
        {
            // Set Playwright to use running directory for browser installation if not already set
            // This prevents the default AppData location from being used
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
            {
                // Setting to "0" makes Playwright use a path relative to the executable
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
                Console.WriteLine("Set PLAYWRIGHT_BROWSERS_PATH=0 (using running directory for browsers)");
            }
            
            // Step 1: Fetch LOF arbitrage data from Server API first
            Console.WriteLine($"\nFetching LOF arbitrage data from API: {apiUrl}/api/data");
            
            var (lofData, shouldWait) = await FetchLofDataFromApi(apiUrl);
            
            // Check if server returned empty with wait message
            if (shouldWait)
            {
                Console.WriteLine("\n⏸️  Server indicated to wait. No arbitrage opportunities available now.");
                Console.WriteLine("   Entering standby mode...");
                return; // Exit to standby mode
            }
            
            if (lofData == null || lofData.Count == 0)
            {
                Console.WriteLine("ℹ️  No LOF data available from API (but no wait message). Entering standby mode...");
                return; // Exit to standby mode
            }
            
            Console.WriteLine($"✓ Fetched {lofData.Count} LOF arbitrage items");
            
            // Step 2: Launch browser and login
            Console.WriteLine("\nInitializing Playwright...");
            
            IPlaywright? playwright = null;
            IBrowser? browser = null;
            
            // Determine if we should run headless
            // In development mode, default to showing the browser UI
            // In production, default to headless unless explicitly set to false
            var isDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development" || 
                                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            var headlessEnv = Environment.GetEnvironmentVariable("HEADLESS");
            bool headless;
            
            if (!string.IsNullOrEmpty(headlessEnv))
            {
                // If HEADLESS is explicitly set, use that value
                headless = headlessEnv.ToLower() != "false";
            }
            else
            {
                // Default behavior: show UI in development, hide in production
                headless = !isDevelopment;
            }
            
            Console.WriteLine($"Running in {(isDevelopment ? "Development" : "Production")} mode");
            Console.WriteLine($"Browser mode: {(headless ? "Headless" : "Visible UI")}");
            
            // Try to initialize Playwright with one retry after installation if needed
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    playwright = await Playwright.CreateAsync();
                    
                    // Launch browser (use headless in production)
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless
                    });
                    break; // Success - exit the retry loop
                }
                catch (Exception ex) when ((ex.Message.Contains("Executable doesn't exist") || 
                                           ex.Message.Contains("Playwright was just installed")) && attempt == 0)
                {
                    // Dispose playwright if it was created before the browser launch failed
                    playwright?.Dispose();
                    playwright = null;
                    
                    Console.WriteLine("\nPlaywright browsers are not installed.");
                    Console.Write("Would you like to install Playwright browsers now? (Y/N): ");
                    
                    var response = Console.ReadLine()?.Trim().ToUpperInvariant();
                    
                    if (response == "Y" || response == "YES")
                    {
                        Console.WriteLine("\nInstalling Playwright browsers...");
                        Console.WriteLine("This may take a few minutes. Please wait...");
                        
                        // Run Playwright install command
                        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                        
                        if (exitCode == 0)
                        {
                            Console.WriteLine("\n✓ Playwright browsers installed successfully!");
                            Console.WriteLine("Retrying browser initialization...\n");
                        }
                        else
                        {
                            Console.WriteLine($"\n✗ Failed to install Playwright browsers. Exit code: {exitCode}");
                            throw;
                        }
                    }
                    else
                    {
                        Console.WriteLine("\nPlaywright installation cancelled.");
                        throw;
                    }
                }
                catch (Exception)
                {
                    // Clean up on any other exception
                    playwright?.Dispose();
                    throw;
                }
            }
            
            if (playwright == null || browser == null)
            {
                throw new InvalidOperationException("Failed to initialize Playwright after installation");
            }
            
            using (playwright)
            await using (browser)
            {
                // Use mobile device emulation since the URLs are mobile pages
                var context = await browser.NewContextAsync(playwright.Devices["iPhone 13 Pro"]);
                var page = await context.NewPageAsync();

            // Step 3: Navigate to login page
            var loginUrl = "https://m.touker.com/account/login/index.htm?loginType=normal&referrer=https://m.touker.com/fd/lofrobot/sceneConstruction#/oauth/custLogin";
            Console.WriteLine($"\nNavigating to login page: {loginUrl}");
            await page.GotoAsync(loginUrl);

            // Wait for page to load
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Step 4: Perform login
            Console.WriteLine("Attempting login...");
            
            try
            {
                // Wait for login form elements - based on actual DOM structure
                await page.WaitForSelectorAsync("input[type='tel'][placeholder='华宝证券客户号']", new PageWaitForSelectorOptions { Timeout = 10000 });
                
                // Fill in credentials using actual DOM selectors
                var usernameSelector = "input[type='tel'][placeholder='华宝证券客户号']";
                var passwordSelector = "input[type='tel'][max='6'].hide_input";
                var checkboxSelector = "div.check-input";
                var submitSelector = "button.hb-btn.hb-btn-primary:has-text('登录')";
                
                await page.FillAsync(usernameSelector, username);
                await page.FillAsync(passwordSelector, password);
                
                // Click the confirmation checkbox before submitting
                Console.WriteLine("Clicking confirmation checkbox...");
                await page.ClickAsync(checkboxSelector);
                
                // Wait a moment for the button to become enabled
                await Task.Delay(500);
                
                Console.WriteLine("Credentials entered, submitting...");
                await page.ClickAsync(submitSelector);
                
                // Wait for navigation after login
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                // Add additional wait to ensure session is fully established
                Console.WriteLine("Waiting for session to be established...");
                await Task.Delay(3000); // 3 seconds
                
                Console.WriteLine("Login submitted successfully");
                Console.WriteLine($"Current URL after login: {page.Url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Login selectors may need adjustment. Error: {ex.Message}");
                Console.WriteLine("Continuing to navigate to main page...");
            }

            // Step 5: Navigate to the main page after login
            var mainUrl = "https://m.touker.com/fd/lofrobot/sceneConstruction";
            Console.WriteLine($"\nNavigating to main page: {mainUrl}");
            
            // Add retry logic for navigation to handle transient errors
            int maxRetries = 3;
            int retryDelay = 2000; // 2 seconds
            bool navigationSuccess = false;
            Exception? lastException = null;
            
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    if (retry > 0)
                    {
                        Console.WriteLine($"Navigation attempt {retry + 1} of {maxRetries}...");
                        await Task.Delay(retryDelay);
                    }
                    
                    // Use a longer timeout and wait for networkidle instead of load
                    await page.GotoAsync(mainUrl, new PageGotoOptions
                    {
                        Timeout = 30000, // 30 seconds
                        WaitUntil = WaitUntilState.NetworkIdle
                    });
                    
                    navigationSuccess = true;
                    Console.WriteLine("Successfully navigated to main page");
                    break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Console.WriteLine($"Navigation attempt {retry + 1} failed: {ex.Message}");
                    
                    // Check if it's an ERR_ABORTED or similar navigation error
                    if (ex.Message.Contains("ERR_ABORTED") || ex.Message.Contains("net::"))
                    {
                        Console.WriteLine("Detected network error, will retry...");
                        // Wait a bit longer for network errors
                        if (retry < maxRetries - 1)
                        {
                            await Task.Delay(retryDelay);
                        }
                    }
                    else
                    {
                        // For other errors, don't retry
                        break;
                    }
                }
            }
            
            if (!navigationSuccess)
            {
                Console.WriteLine($"\n✗ Failed to navigate to main page after {maxRetries} attempts");
                Console.WriteLine($"Last error: {lastException?.Message}");
                Console.WriteLine("\nPossible reasons:");
                Console.WriteLine("  - Login session may have expired or failed");
                Console.WriteLine("  - Network connectivity issues");
                Console.WriteLine("  - The website may be blocking automated access");
                Console.WriteLine($"\nCurrent page URL: {page.Url}");
                throw new InvalidOperationException("Failed to navigate to main page", lastException);
            }
            
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine("Page URL: " + page.Url);

            // Step 6: Fetch stock holder list from the API
            Console.WriteLine("\nFetching stock holder list...");
            var stockHolderList = await FetchStockHolderList(page);
            
            if (stockHolderList.Count == 0)
            {
                throw new InvalidOperationException("No stock holders found. Cannot proceed with arbitrage creation.");
            }
            
            Console.WriteLine($"Found {stockHolderList.Count} stock holders:");
            for (int i = 0; i < stockHolderList.Count; i++)
            {
                Console.WriteLine($"  - {stockHolderList[i]} ({i + 1}/{stockHolderList.Count})");
            }

            // Step 7: Calculate total investment based on Amount field
            decimal totalInvestment = 0;
            
            Console.WriteLine($"\nInvestment Calculation (Max per holder: {maxInvestmentPerHolder} yuan):");
            
            foreach (var item in lofData)
            {
                // Use the Amount field from the API (already converted from ApplyStatus)
                var applyLimit = item.Amount;
                // Take the minimum of apply_status limit and configured max investment per holder
                var effectiveLimit = Math.Min(applyLimit, maxInvestmentPerHolder);
                var investmentPerFund = stockHolderList.Count * effectiveLimit;
                totalInvestment += investmentPerFund;
                
                Console.WriteLine($"  - Fund: {item.LofFund}");
                Console.WriteLine($"    Apply Status: {item.ApplyStatus}, Amount: {applyLimit} yuan");
                Console.WriteLine($"    Effective Limit: {effectiveLimit} yuan per holder");
                Console.WriteLine($"    Investment: {stockHolderList.Count} holders × {effectiveLimit} yuan = {investmentPerFund} yuan");
            }
            
            Console.WriteLine($"\nTotal Investment Summary:");
            Console.WriteLine($"  - Number of Stock Holders: {stockHolderList.Count}");
            Console.WriteLine($"  - Number of LOF Funds: {lofData.Count}");
            Console.WriteLine($"  - Total Investment Amount: {totalInvestment} yuan");

            // Step 8: Create arbitrage request
            var request = new CreateArbitrageRequest
            {
                StockHolderList = stockHolderList,
                PurchaseTime = purchaseTime,
                List = lofData,
                Password = password
            };

            Console.WriteLine($"\nCreating arbitrage with:");
            Console.WriteLine($"  - Stock Holders: {stockHolderList.Count}");
            Console.WriteLine($"  - Purchase Time: {purchaseTime}");
            Console.WriteLine($"  - LOF Items: {lofData.Count}");

            // Step 9: Send POST request to create arbitrage
            var success = await CreateArbitrage(page, request);
            
            if (success)
            {
                Console.WriteLine("\n✅ Arbitrage created successfully! All green.");
            }
            else
            {
                throw new InvalidOperationException("Failed to create arbitrage. Check logs for details.");
            }

            // Keep browser open for a moment to see the result
            if (!headless)
            {
                Console.WriteLine("\nPress Enter to continue to standby mode...");
                Console.ReadLine();
            }
            else
            {
                // In headless mode, wait a bit before closing
                await Task.Delay(5000);
            }
            } // Close using blocks
        }
        catch (Exception)
        {
            // Re-throw to be handled by the caller
            throw;
        }
    }
    
    private static async Task EnterStandbyModeAsync()
    {
        Console.WriteLine("\n" + new string('-', 80));
        Console.WriteLine("Entering standby mode...");
        
        // Get Shanghai time (UTC+8)
        TimeZoneInfo shanghaiTimeZone;
        try
        {
            // Try Windows timezone ID first
            shanghaiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Try IANA timezone ID (for Linux/Mac)
                shanghaiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback: Create a custom timezone for UTC+8
                Console.WriteLine("Warning: Shanghai timezone not found. Using UTC+8 offset.");
                shanghaiTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "Shanghai Custom", 
                    TimeSpan.FromHours(8), 
                    "Shanghai Time", 
                    "Shanghai Time");
            }
        }
        
        var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, shanghaiTimeZone);
        
        // Target time is 14:20 today
        var targetTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 14, 20, 0);
        
        // If current time is already past 14:20, target next day's 14:20
        if (currentTime >= targetTime)
        {
            targetTime = targetTime.AddDays(1);
            Console.WriteLine($"Current time ({currentTime:HH:mm:ss}) is past today's 14:20. Will wait until tomorrow.");
        }
        
        var waitDuration = targetTime - currentTime;
        
        Console.WriteLine($"Current Shanghai time: {currentTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Next arbitrage check: {targetTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Sleep duration: {waitDuration.Days} days, {waitDuration.Hours} hours, {waitDuration.Minutes} minutes, {waitDuration.Seconds} seconds");
        Console.WriteLine(new string('-', 80));
        
        // Sleep until target time
        await Task.Delay(waitDuration);
        
        Console.WriteLine($"\n⏰ Waking up at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private static string GetConfig(string[] args, string envVarName, int argIndex, string? defaultValue = null)
    {
        // First check command line arguments
        if (args.Length > argIndex && !string.IsNullOrEmpty(args[argIndex]))
        {
            return args[argIndex];
        }

        // Then check environment variables
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue))
        {
            return envValue;
        }

        // Return default value if provided
        return defaultValue ?? string.Empty;
    }

    private static async Task<List<string>> FetchStockHolderList(IPage page)
    {
        try
        {
            // Use Playwright's API request context to make the request with the authenticated session
            var response = await page.Context.APIRequest.GetAsync("https://m.touker.com/lofarbitrage/lof/arbitrage/queryStockHolderNoList.do");
            
            if (response.Ok)
            {
                var json = await response.TextAsync();
                Console.WriteLine($"Stock holder response: {json}");
                
                var data = JsonConvert.DeserializeObject<StockHolderResponse>(json);
                
                if (data?.Success == true && data.Result != null)
                {
                    return [.. data.Result.Select(s => s.StockHolderNo)];
                }
            }
            else
            {
                Console.WriteLine($"Failed to fetch stock holders. Status: {response.Status}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock holders: {ex.Message}");
        }
        
        return [];
    }

    private static async Task<(List<LofArbitrageData>?, bool shouldWait)> FetchLofDataFromApi(string baseUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync($"{baseUrl}/api/data");
            
            // Check response headers for wait message
            bool shouldWait = false;
            if (response.Headers.TryGetValues("X-Wait-Message", out var waitMessages))
            {
                var waitMessage = waitMessages.FirstOrDefault();
                if (!string.IsNullOrEmpty(waitMessage))
                {
                    Console.WriteLine($"Server wait message: {waitMessage}");
                    shouldWait = true;
                }
            }
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<List<LofArbitrageData>>(json);
                
                // If data is empty or null, check if we should wait
                if ((data == null || data.Count == 0) && shouldWait)
                {
                    return (null, true);
                }
                
                return (data, false);
            }
            else
            {
                Console.WriteLine($"API returned status code: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error content: {errorContent}");
                
                // Check if error content suggests waiting
                if (errorContent.Contains("wait", StringComparison.OrdinalIgnoreCase) || 
                    errorContent.Contains("in progress", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, true);
                }
                
                return (null, shouldWait);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch data from API: {ex.Message}");
            return (null, false);
        }
    }

    private static async Task<bool> CreateArbitrage(IPage page, CreateArbitrageRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            Console.WriteLine($"\nSending POST request to createLofArbitrage.do");
            Console.WriteLine($"Payload: {json}");
            
            // Use Playwright's API request context to make the POST request with the authenticated session
            var response = await page.Context.APIRequest.PostAsync(
                "https://m.touker.com/lofarbitrage/lof/arbitrage/createLofArbitrage.do",
                new APIRequestContextOptions
                {
                    DataObject = request
                });
            
            var responseText = await response.TextAsync();
            Console.WriteLine($"Response status: {response.Status}");
            Console.WriteLine($"Response body: {responseText}");
            
            return response.Ok;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating arbitrage: {ex.Message}");
            return false;
        }
    }
}

