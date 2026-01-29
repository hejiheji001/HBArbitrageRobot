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
        var pwd = GetConfig(args, "PWD", 3); // Transaction password (required, no default)
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

        if (string.IsNullOrEmpty(pwd))
        {
            Console.WriteLine("ERROR: Transaction password (PWD) is required.");
            Console.WriteLine("  Set via command line argument or PWD environment variable");
            return;
        }

        try
        {
            // Step 1: Launch browser and login
            Console.WriteLine("\nInitializing Playwright...");
            using var playwright = await Playwright.CreateAsync();
            
            // Launch browser (use headless in production)
            var headless = Environment.GetEnvironmentVariable("HEADLESS") != "false";
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = headless
            });

            var context = await browser.NewContextAsync();
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
                
                Console.WriteLine("Login submitted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Login selectors may need adjustment. Error: {ex.Message}");
                Console.WriteLine("Continuing to navigate to main page...");
            }

            // Step 4: Navigate to the main page after login
            var mainUrl = "https://m.touker.com/fd/lofrobot/sceneConstruction";
            Console.WriteLine($"\nNavigating to main page: {mainUrl}");
            await page.GotoAsync(mainUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine("Page URL: " + page.Url);

            // Step 5: Fetch stock holder list from the API
            Console.WriteLine("\nFetching stock holder list...");
            var stockHolderList = await FetchStockHolderList(page);
            
            if (stockHolderList.Count == 0)
            {
                Console.WriteLine("Warning: No stock holders found. Cannot proceed with arbitrage creation.");
                return;
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
                return;
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
                Pwd = pwd
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
                Console.WriteLine("\nPress Enter to close browser...");
                Console.ReadLine();
            }
            else
            {
                // In headless mode, wait a bit before closing
                await Task.Delay(5000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
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

