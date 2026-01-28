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

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("ERROR: Username and password are required.");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Testing: dotnet run <username> <password> [api_url]");
            Console.WriteLine("  Production: Set USERNAME and PASSWORD environment variables");
            return;
        }

        try
        {
            // Step 1: Read data from API
            Console.WriteLine($"Fetching data from API: {apiUrl}/api/data");
            var apiData = await FetchDataFromApi(apiUrl);
            
            if (apiData != null)
            {
                Console.WriteLine($"Data fetched successfully:");
                Console.WriteLine($"  Source: {apiData.Source}");
                Console.WriteLine($"  Fetched At: {apiData.FetchedAt}");
                Console.WriteLine($"  Content Length: {apiData.Content?.Length ?? 0} characters");
            }
            else
            {
                Console.WriteLine("Warning: No data available from API yet. Continuing with login...");
            }

            // Step 2: Launch browser and login
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

            // Step 3: Navigate to login page
            var loginUrl = "https://m.touker.com/fd/lofrobot/sceneConstruction#/oauth/custLogin";
            Console.WriteLine($"\nNavigating to login page: {loginUrl}");
            await page.GotoAsync(loginUrl);

            // Wait for page to load
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Step 4: Perform login (this is a placeholder - actual selectors need to be adjusted)
            Console.WriteLine("Attempting login...");
            
            try
            {
                // These selectors are placeholders and should be updated based on actual page structure
                // Wait for login form elements
                await page.WaitForSelectorAsync("input[type='text'], input[type='tel'], input[name*='user'], input[name*='phone']", new PageWaitForSelectorOptions { Timeout = 10000 });
                
                // Fill in credentials (selectors need to be adjusted for actual page)
                var usernameSelector = "input[type='text'], input[type='tel'], input[name*='user'], input[name*='phone']";
                var passwordSelector = "input[type='password']";
                var submitSelector = "button[type='submit'], button:has-text('登录'), button:has-text('Login')";
                
                await page.FillAsync(usernameSelector, username);
                await page.FillAsync(passwordSelector, password);
                
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

            // Step 5: Navigate to the main page after login
            var mainUrl = "https://m.touker.com/fd/lofrobot/sceneConstruction";
            Console.WriteLine($"\nNavigating to main page: {mainUrl}");
            await page.GotoAsync(mainUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Console.WriteLine("\nClient operation completed successfully!");
            Console.WriteLine("Page URL: " + page.Url);
            
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

    private static async Task<ArbitrageData?> FetchDataFromApi(string baseUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.GetAsync($"{baseUrl}/api/data");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ArbitrageData>(json);
            }
            else
            {
                Console.WriteLine($"API returned status code: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch data from API: {ex.Message}");
            return null;
        }
    }
}

