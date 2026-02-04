using Client.Models;
using Microsoft.Playwright;

namespace Client.Services;

/// <summary>
/// Orchestrator service that coordinates the arbitrage workflow
/// </summary>
public class WorkflowOrchestrator(
    ConfigurationService config,
    BrowserService browserService,
    AuthenticationService authService,
    ArbitrageService arbitrageService)
{

    /// <summary>
    /// Run the complete arbitrage workflow
    /// </summary>
    /// <param name="lofData">LOF arbitrage data</param>
    public async Task RunArbitrageWorkflowAsync(List<LofArbitrageData> lofData)
    {
        // Initialize browser and login
        // In CLI mode, we explicitly pass headless=true
        // In development/testing, we can pass headless=false to see the browser
        var (playwright, browser) = await browserService.InitializeBrowserAsync(config.Headless);
        
        using (playwright)
        {
            await using (browser)
            {
                var context = await browser.NewContextAsync(playwright.Devices["iPhone 13 Pro"]);
                var page = await context.NewPageAsync();

                // Login
                await NavigateToLoginAndAuthenticateAsync(page);

                // Navigate to main page
                await NavigateToMainPageAsync(page);

                // Fetch stock holder list
                var stockHolderList = await FetchAndValidateStockHoldersAsync(page);

                // Calculate investments
                ArbitrageService.CalculateInvestments(lofData, stockHolderList, config.MaxInvestmentPerHolder);

                // Create arbitrage orders
                await CreateArbitrageOrdersAsync(page, lofData, stockHolderList);

                // Wait for operations to complete
                await Task.Delay(5000);
            }
        }
    }

    private async Task NavigateToLoginAndAuthenticateAsync(IPage page)
    {
        var loginUrl = "https://m.touker.com/account/login/index.htm?loginType=normal";
        Console.WriteLine($"\nNavigating to login page: {loginUrl}");
        
        await page.GotoAsync(loginUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        await authService.LogInAsync(page);
    }

    private async Task NavigateToMainPageAsync(IPage page)
    {
        var mainUrl = "https://m.touker.com/fd/lofrobot/sceneConstruction";
        await BrowserService.NavigateWithRetryAsync(page, mainUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Console.WriteLine("Page URL: " + page.Url);
    }

    private async Task<List<string>> FetchAndValidateStockHoldersAsync(IPage page)
    {
        Console.WriteLine("\nFetching stock holder list...");
        var stockHolderList = await arbitrageService.FetchStockHolderListAsync(page);

        if (stockHolderList.Count == 0)
        {
            throw new InvalidOperationException("No stock holders found. Cannot proceed with arbitrage creation.");
        }

        Console.WriteLine($"Found {stockHolderList.Count} stock holders:");
        for (int i = 0; i < stockHolderList.Count; i++)
        {
            Console.WriteLine($"  - {stockHolderList[i]} ({i + 1}/{stockHolderList.Count})");
        }

        return stockHolderList;
    }

    private async Task CreateArbitrageOrdersAsync(IPage page, List<LofArbitrageData> lofData, List<string> stockHolderList)
    {
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
            PurchaseTime = config.PurchaseTime,
            List = lofPayload,
            Password = config.Password
        };

        Console.WriteLine($"\nCreating arbitrage with:");
        Console.WriteLine($"  - Stock Holders: {stockHolderList.Count}");
        Console.WriteLine($"  - Purchase Time: {config.PurchaseTime}");
        Console.WriteLine($"  - LOF Items: {lofData.Count}");

        var success = await arbitrageService.CreateArbitrageAsync(page, request);

        if (success)
        {
            Console.WriteLine("\n✅ Arbitrage created successfully! All green.");
        }
        else
        {
            throw new InvalidOperationException("Failed to create arbitrage. Check logs for details.");
        }
    }
}
