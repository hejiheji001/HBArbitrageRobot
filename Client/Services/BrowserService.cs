using Microsoft.Playwright;

namespace Client.Services;

/// <summary>
/// Service for managing browser lifecycle and Playwright operations
/// </summary>
public class BrowserService
{
    /// <summary>
    /// Initialize Playwright and launch browser with retry logic
    /// </summary>
    public async Task<(IPlaywright playwright, IBrowser browser)> InitializeBrowserAsync(bool? headless = true)
    {
        ConfigurePlaywrightEnvironment();
        
        var useHeadless = headless ?? DetermineHeadlessMode();
        
        Console.WriteLine($"Browser mode: {(useHeadless ? "Headless" : "Visible UI")}");

        return await LaunchBrowserWithRetryAsync(useHeadless);
    }

    /// <summary>
    /// Navigate to a URL with retry logic to handle transient errors
    /// </summary>
    public async Task NavigateWithRetryAsync(IPage page, string url, int maxRetries = 3, int retryDelayMs = 2000)
    {
        Console.WriteLine($"\nNavigating to: {url}");
        
        bool navigationSuccess = false;
        Exception? lastException = null;

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                if (retry > 0)
                {
                    Console.WriteLine($"Navigation attempt {retry + 1} of {maxRetries}...");
                    await Task.Delay(retryDelayMs);
                }

                // Use a longer timeout and wait for networkidle instead of load
                await page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout = 30000, // 30 seconds
                    WaitUntil = WaitUntilState.NetworkIdle
                });

                navigationSuccess = true;
                Console.WriteLine("Successfully navigated to page");
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
                    if (retry < maxRetries - 1)
                    {
                        await Task.Delay(retryDelayMs);
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
            Console.WriteLine($"\n✗ Failed to navigate after {maxRetries} attempts");
            Console.WriteLine($"Last error: {lastException?.Message}");
            Console.WriteLine("\nPossible reasons:");
            Console.WriteLine("  - Login session may have expired or failed");
            Console.WriteLine("  - Network connectivity issues");
            Console.WriteLine("  - The website may be blocking automated access");
            Console.WriteLine($"\nCurrent page URL: {page.Url}");
            throw new InvalidOperationException("Failed to navigate", lastException);
        }
    }

    private void ConfigurePlaywrightEnvironment()
    {
        // Set Playwright to use running directory for browser installation if not already set
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
            Console.WriteLine("Set PLAYWRIGHT_BROWSERS_PATH=0 (using running directory for browsers)");
        }
    }

    private bool DetermineHeadlessMode()
    {
        var isDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development" ||
                            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        var headlessEnv = Environment.GetEnvironmentVariable("HEADLESS");
        bool headless;

        if (!string.IsNullOrEmpty(headlessEnv))
        {
            // If HEADLESS is explicitly set, use that value
            headless = !headlessEnv.Equals("false", StringComparison.CurrentCultureIgnoreCase);
        }
        else
        {
            // Default behavior: show UI in development, hide in production
            headless = !isDevelopment;
        }

        Console.WriteLine($"Running in {(isDevelopment ? "Development" : "Production")} mode");
        
        return headless;
    }

    private async Task<(IPlaywright, IBrowser)> LaunchBrowserWithRetryAsync(bool headless)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        // Try to initialize Playwright with one retry after installation if needed
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                playwright = await Playwright.CreateAsync();

                // Launch browser
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

                await InstallPlaywrightBrowsersAsync();
            }
        }

        if (playwright == null || browser == null)
        {
            throw new InvalidOperationException("Failed to initialize Playwright after installation");
        }

        return (playwright, browser);
    }

    private async Task InstallPlaywrightBrowsersAsync()
    {
        Console.WriteLine("\nPlaywright browsers are not installed.");
        Console.Write("Would you like to install Playwright browsers now? (Y/N): ");

        var response = Console.ReadLine()?.Trim().ToUpperInvariant();

        if (response == "Y" || response == "YES")
        {
            Console.WriteLine("\nInstalling Playwright browsers...");
            Console.WriteLine("This may take a few minutes. Please wait...");

            // Run Playwright install command
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

            if (exitCode == 0)
            {
                Console.WriteLine("\n✓ Playwright browsers installed successfully!");
                Console.WriteLine("Retrying browser initialization...\n");
            }
            else
            {
                Console.WriteLine($"\n✗ Failed to install Playwright browsers. Exit code: {exitCode}");
                throw new InvalidOperationException("Playwright installation failed");
            }
        }
        else
        {
            Console.WriteLine("\nPlaywright installation cancelled.");
            throw new InvalidOperationException("Playwright installation cancelled by user");
        }
    }
}
