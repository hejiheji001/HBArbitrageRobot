using Client.Models;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace Client.Services;

/// <summary>
/// Service for handling arbitrage operations on the trading platform
/// </summary>
public class ArbitrageService
{
    private readonly AuthenticationService _authService;

    public ArbitrageService(AuthenticationService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Fetch stock holder list from the trading platform
    /// </summary>
    public async Task<List<string>> FetchStockHolderListAsync(IPage page)
    {
        try
        {
            var url = "https://m.touker.com/lofarbitrage/lof/arbitrage/queryStockHolderNoList.do";
            var response = await FetchDataAsync(page, url, HttpMethod.Get);

            Console.WriteLine($"Stock holder response: {response}");

            if (response.Contains("未登陆"))
            {
                await _authService.LogInAsync(page);
                response = await FetchDataAsync(page, url, HttpMethod.Get);
            }

            var data = JsonConvert.DeserializeObject<StockHolderResponse>(response);

            if (data?.Success == true && data.Result != null)
            {
                return [.. data.Result.Select(s => s.StockHolderNo)];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock holders: {ex.Message}");
        }

        return [];
    }

    /// <summary>
    /// Create arbitrage orders on the trading platform
    /// </summary>
    public async Task<bool> CreateArbitrageAsync(IPage page, CreateArbitrageRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var header = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" }
            };

            Console.WriteLine($"Sending POST request to createLofArbitrage.do");
            // Note: Not logging payload as it contains sensitive password information

            var payload = new APIRequestContextOptions
            {
                DataString = json,
                Headers = header
            };

            var response = await FetchDataAsync(page, 
                "https://m.touker.com/lofarbitrage/lof/arbitrage/createLofArbitrage.do", 
                HttpMethod.Post, payload);
            Console.WriteLine($"Response body: {response}");

            if (response.Contains("申购数量不在配置允许范围"))
            {
                await HandleQuantityLimitExceededAsync(page, request, header);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating arbitrage: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Calculate investment amounts based on LOF data and constraints
    /// </summary>
    public decimal CalculateInvestments(List<LofArbitrageData> lofData, List<string> stockHolderList, decimal maxInvestmentPerHolder)
    {
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

            item.Amount = effectiveLimit; // Update for payload

            Console.WriteLine($"  - Fund: {item.LofFund}");
            Console.WriteLine($"  - Name: {item.Name}");
            Console.WriteLine($"    Apply Status: {item.ApplyStatus}, Amount: {applyLimit} yuan");
            Console.WriteLine($"    Effective Limit: {effectiveLimit} yuan per holder");
            Console.WriteLine($"    Investment: {stockHolderList.Count} holders × {effectiveLimit} yuan = {investmentPerFund} yuan");
        }

        Console.WriteLine($"\nTotal Investment Summary:");
        Console.WriteLine($"  - Number of Stock Holders: {stockHolderList.Count}");
        Console.WriteLine($"  - Number of LOF Funds: {lofData.Count}");
        Console.WriteLine($"  - Total Investment Amount: {totalInvestment} yuan");

        return totalInvestment;
    }

    private async Task<string> FetchDataAsync(IPage page, string url, HttpMethod method, APIRequestContextOptions? payload = null)
    {
        var result = string.Empty;
        
        try
        {
            var response = method.Method switch
            {
                "POST" => await page.Context.APIRequest.PostAsync(url, payload),
                "GET" => await page.Context.APIRequest.GetAsync(url),
                _ => throw new NotImplementedException(),
            };

            if (response.Ok)
            {
                result = await response.TextAsync();
            }
            else
            {
                Console.WriteLine($"Failed to fetch data from {url} @ Status: {response.Status}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching data from {url} @ {ex.Message}");
        }

        return result;
    }

    private async Task HandleQuantityLimitExceededAsync(IPage page, CreateArbitrageRequest request, Dictionary<string, string> header)
    {
        Console.WriteLine("Splitting request into multiple smaller requests due to quantity limit...");

        await Task.WhenAll(request.List.Select(async item =>
        {
            await ProcessSingleFundWithSplittingAsync(page, request, item, header);
        }));

        PrintFinalInvestmentSummary(request);
    }

    private async Task ProcessSingleFundWithSplittingAsync(IPage page, CreateArbitrageRequest request, LofArbitragePayload item, Dictionary<string, string> header)
    {
        var innerPayload = new APIRequestContextOptions
        {
            DataString = JsonConvert.SerializeObject(request with
            {
                List = [item]
            }),
            Headers = header
        };

        var resp = await FetchDataAsync(page, 
            "https://m.touker.com/lofarbitrage/lof/arbitrage/createLofArbitrage.do", 
            HttpMethod.Post, innerPayload);
        Console.WriteLine($"Processing fund: {item.LofFund} with Amount: {item.LofArbitrage.PurchaseQuantity} @ {resp}");

        await Task.Delay(1000); // Small delay between requests
        
        while (resp.Contains("申购数量不在配置允许范围"))
        {
            var minifiedAmount = int.Parse(item.LofArbitrage.PurchaseQuantity) / 10 - 1;

            if (minifiedAmount < 1)
            {
                Console.WriteLine("Cannot split further, minimum quantity reached.");
                break;
            }

            item.LofArbitrage.PurchaseQuantity = minifiedAmount.ToString();

            var minifiedPayload = new APIRequestContextOptions
            {
                DataString = JsonConvert.SerializeObject(request with
                {
                    List = [item]
                }),
                Headers = header
            };

            resp = await FetchDataAsync(page, 
                "https://m.touker.com/lofarbitrage/lof/arbitrage/createLofArbitrage.do", 
                HttpMethod.Post, minifiedPayload);
            Console.WriteLine($"Processing fund: {item.LofFund} with Splitted Amount: {item.LofArbitrage.PurchaseQuantity} @ {resp}");
            await Task.Delay(1000); // Small delay between requests
        }
    }

    private void PrintFinalInvestmentSummary(CreateArbitrageRequest request)
    {
        Console.WriteLine("\n=== Final Investment Summary After Splitting ===");
        decimal actualTotalInvestment = 0;

        foreach (var item in request.List)
        {
            var finalQuantity = int.Parse(item.LofArbitrage.PurchaseQuantity);
            var investmentPerFund = request.StockHolderList.Count * finalQuantity;
            actualTotalInvestment += investmentPerFund;

            Console.WriteLine($"  - Fund: {item.LofFund}");
            Console.WriteLine($"    Final Purchase Quantity: {finalQuantity} yuan per holder");
            Console.WriteLine($"    Investment: {request.StockHolderList.Count} holders × {finalQuantity} yuan = {investmentPerFund} yuan");
        }

        Console.WriteLine($"\n  Total Investment After Splitting: {actualTotalInvestment} yuan");
        Console.WriteLine("================================================\n");
    }
}
