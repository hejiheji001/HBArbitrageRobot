using Microsoft.Playwright;
using Newtonsoft.Json;
using Client.Models;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("HBArbitrageRobot Client Starting...");

        // Parse arguments or environment variables for credentials
        var username = GetConfig(args, "USERNAME", 0);
        var password = GetConfig(args, "PASSWORD", 1);
        var apiUrl = GetConfig(args, "API_URL", 2, "http://localhost:5000");
        var purchaseTime = GetConfig(args, "PURCHASE_TIME", 4, "14:15");
        
        // Configurable max investment per stock holder per fund (in yuan)
        var maxInvestmentPerHolder = decimal.Parse(GetConfig(args, "MAX_INVESTMENT", 5, "1000"));
        
        // Configurable retry delay in seconds (default: 60 seconds)
        var retryDelaySeconds = int.Parse(GetConfig(args, "RETRY_DELAY_SECONDS", 6, "60"));

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("ERROR: Username and password are required.");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Testing: dotnet run <username> <password> [api_url] [pwd] [purchase_time]");
            Console.WriteLine("  Production: Set USERNAME, PASSWORD, PWD, PURCHASE_TIME environment variables");
            Console.WriteLine("\nWaiting before retrying...");
            await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            // Don't exit - configuration might be available on next retry
            await Main(args); // Recursive retry
            return;
        }

        // Infinite loop to handle errors and retry
        while (true)
        {
            try
            {
                await RunArbitrageWorkflowAsync(username, password, apiUrl, purchaseTime, maxInvestmentPerHolder);
                
                // If successful, wait and retry
                Console.WriteLine($"\n✓ Workflow completed successfully. Waiting {retryDelaySeconds} seconds before next run...");
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine($"\nRecovering from error. Waiting {retryDelaySeconds} seconds before retry...");
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }
    }

    private static async Task RunArbitrageWorkflowAsync(string username, string password, string apiUrl, string purchaseTime, decimal maxInvestmentPerHolder)
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
            
            // Step 1: Launch browser and login
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
                            throw new InvalidOperationException($"Failed to install Playwright browsers. Exit code: {exitCode}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\nPlaywright installation cancelled.");
                        throw new InvalidOperationException("Playwright installation cancelled by user");
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

            // Step 2: Navigate to login page
            var loginUrl = "https://m.touker.com/account/login/index.htm?loginType=normal&referrer=https://m.touker.com/fd/lofrobot/sceneConstruction#/oauth/custLogin";
            Console.WriteLine($"\nNavigating to login page: {loginUrl}");
            await page.GotoAsync(loginUrl);

            // Wait for page to load
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Step 3: Perform login
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

            // Step 4: Navigate to the main page after login
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
                Console.WriteLine("Please check if login was successful.");
                throw new InvalidOperationException($"Failed to navigate to main page after {maxRetries} attempts: {lastException?.Message}");
            }
            
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine("Page URL: " + page.Url);

            // Step 5: Fetch stock holder list from the API
            Console.WriteLine("\nFetching stock holder list...");
            var stockHolderList = await FetchStockHolderList(page);
            
            if (stockHolderList.Count == 0)
            {
                Console.WriteLine("Warning: No stock holders found. Cannot proceed with arbitrage creation.");
                throw new InvalidOperationException("No stock holders found");
            }
            
            Console.WriteLine($"Found {stockHolderList.Count} stock holders:");
            for (int i = 0; i < stockHolderList.Count; i++)
            {
                Console.WriteLine($"  - {stockHolderList[i]} ({i + 1}/{stockHolderList.Count})");
            }

            // Step 6: Fetch LOF arbitrage data from Server API
            Console.WriteLine($"\nFetching LOF arbitrage data from API: {apiUrl}/api/data");
            var lofData = await FetchLofDataFromApi(apiUrl);
            
            if (lofData == null || lofData.Count == 0)
            {
                Console.WriteLine("Warning: No LOF data available from API.");
                throw new InvalidOperationException("No LOF data available from API");
            }
            
            Console.WriteLine($"Fetched {lofData.Count} LOF arbitrage items");
            
            // Calculate total investment based on Amount field
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

            // Step 7: Create arbitrage request
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

            // Step 8: Send POST request to create arbitrage
            var success = await CreateArbitrage(page, request);
            
            if (success)
            {
                Console.WriteLine("\n✓ Arbitrage created successfully!");
            }
            else
            {
                Console.WriteLine("\n✗ Failed to create arbitrage. Check logs for details.");
            }

            // Keep browser open for a moment to see the result
            if (!headless)
            {
                Console.WriteLine("\nPress Enter to continue to next cycle...");
                Console.ReadLine();
            }
            else
            {
                // In headless mode, wait a bit before closing
                await Task.Delay(5000);
            }
            } // Close using blocks
        }
        catch (Exception ex)
        {
            // Let exception bubble up to be caught by the main loop
            throw new InvalidOperationException($"Workflow failed: {ex.Message}", ex);
        }
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

    private static async Task<List<LofArbitrageData>?> FetchLofDataFromApi(string baseUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync($"{baseUrl}/api/data");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<LofArbitrageData>>(json);
            }
            else
            {
                Console.WriteLine($"API returned status code: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error content: {errorContent}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch data from API: {ex.Message}");
            return null;
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

