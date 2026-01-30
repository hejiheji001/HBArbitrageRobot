using Server.Models;
using System.Text.RegularExpressions;

namespace Server.Services;

public partial class DataFetchService(
    ILogger<DataFetchService> logger,
    DataStore dataStore,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private readonly SemaphoreSlim _fetchSemaphore = new(1, 1);
    
    // Constants for filtering criteria
    private const string APPLY_STATUS_SUSPENDED = "暂停申购";
    private const decimal MIN_DISCOUNT_RATE = 10m;
    private const decimal UNLIMITED_AMOUNT = 10000m;
    
    // Regex pattern for parsing ApplyStatus (e.g., "限10", "限1万", "限1千")
    private static readonly Regex ApplyStatusWithUnitRegex = InvestAmountRegex();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Data Fetch Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = GetShanghaiTime();
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 14, 0, 0);

            // If we've passed today's 14:00, schedule for tomorrow
            if (now >= targetTime)
            {
                targetTime = targetTime.AddDays(1);
            }

            var delay = targetTime - now;
            logger.LogInformation("Next fetch scheduled at: {TargetTime} (in {Delay})", targetTime, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                
                // Fetch data immediately after delay
                await FetchDataAsync(stoppingToken);
                
                // After fetching, wait until next day's 14:00
                // Calculate delay to next day's 14:00 from current time
                var afterFetchTime = GetShanghaiTime();
                var nextTargetTime = new DateTime(afterFetchTime.Year, afterFetchTime.Month, afterFetchTime.Day, 14, 0, 0).AddDays(1);
                var nextDelay = nextTargetTime - afterFetchTime;
                
                logger.LogInformation("Next fetch scheduled at: {TargetTime} (in {Delay})", nextTargetTime, nextDelay);
                await Task.Delay(nextDelay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("Data fetch service is stopping");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in data fetch loop");
                // Wait 1 minute before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task FetchDataAsync(CancellationToken cancellationToken)
    {
        // Use semaphore to ensure only one fetch operation runs at a time
        if (!await _fetchSemaphore.WaitAsync(0, cancellationToken))
        {
            logger.LogWarning("Fetch already in progress, skipping this request");
            return;
        }

        try
        {
            await FetchDataInternalAsync(cancellationToken);
        }
        finally
        {
            _fetchSemaphore.Release();
        }
    }

    public async Task TriggerManualFetchAsync(CancellationToken cancellationToken)
    {
        // Use semaphore to ensure only one fetch operation runs at a time
        if (!await _fetchSemaphore.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A data fetch operation is already in progress");
        }

        try
        {
            await FetchDataInternalAsync(cancellationToken);
        }
        finally
        {
            _fetchSemaphore.Release();
        }
    }

    private async Task FetchDataInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Fetching data from jisilu.cn at {Time}", GetShanghaiTime());

            const string baseUrl = "https://www.jisilu.cn/data";
            var lofUrls = new[]
            {
                "/lof/index_lof_list/?only_owned=&rp=25",
                "/lof/stock_lof_list/?only_owned=&rp=25",
                "/qdii/qdii_list/E?only_lof=y&rp=22",
                "/qdii/qdii_list/C?rp=22"
            };

            var allFunds = new Dictionary<string, JisiluCell>(); // Use dictionary to prevent duplicates

            // Create HttpClient with proper headers for each request
            using var httpClient = httpClientFactory.CreateClient("DataFetchClient");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Fetch data from all LOF URLs
            foreach (var lofUrl in lofUrls)
            {
                try
                {
                    // Generate timestamp parameter
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var timestampParam = $"___jsl=LST___t={timestamp}";
                    var separator = lofUrl.Contains('?') ? "&" : "?";
                    var fullUrl = $"{baseUrl}{lofUrl}{separator}{timestampParam}";

                    logger.LogInformation("Fetching from: {Url}", fullUrl);

                    var response = await httpClient.GetAsync(fullUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var jisiluResponse = System.Text.Json.JsonSerializer.Deserialize<JisiluResponse>(content, options);

                    if (jisiluResponse?.Rows != null)
                    {
                        logger.LogInformation("Received {Count} rows from {Url}", jisiluResponse.Rows.Count, lofUrl);
                        
                        // Add funds to dictionary to prevent duplicates
                        foreach (var row in jisiluResponse.Rows)
                        {
                            if (!string.IsNullOrEmpty(row.Cell.FundId) && !allFunds.ContainsKey(row.Cell.FundId))
                            {
                                allFunds[row.Cell.FundId] = row.Cell;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to fetch data from {Url}", lofUrl);
                    // Continue with other URLs even if one fails
                }
            }

            // Filter funds: discount_rt > 10% AND apply_status != "暂停申购"
            var filteredFunds = allFunds.Values.Where(fund =>
            {
                // Parse discount_rt (e.g., "41.74%" -> 41.74)
                if (string.IsNullOrEmpty(fund.DiscountRt) || fund.DiscountRt == "-")
                {
                    logger.LogDebug("Fund {FundId} skipped: discount_rt is empty or '-'", fund.FundId);
                    return false;
                }

                var discountStr = fund.DiscountRt.Replace("%", "").Trim();
                if (!decimal.TryParse(discountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var discountValue))
                {
                    logger.LogDebug("Fund {FundId} skipped: cannot parse discount_rt '{DiscountRt}'", fund.FundId, fund.DiscountRt);
                    return false;
                }

                var meetsDiscountCriteria = discountValue > MIN_DISCOUNT_RATE;
                var meetsApplyCriteria = fund.ApplyStatus != APPLY_STATUS_SUSPENDED;

                if (!meetsDiscountCriteria)
                {
                    logger.LogDebug("Fund {FundId} ({FundNm}) skipped: discount_rt {Discount}% <= {MinDiscount}%", 
                        fund.FundId, fund.FundNm, discountValue, MIN_DISCOUNT_RATE);
                }

                if (!meetsApplyCriteria)
                {
                    logger.LogDebug("Fund {FundId} ({FundNm}) skipped: apply_status '{Status}' == '{SuspendedStatus}'", 
                        fund.FundId, fund.FundNm, fund.ApplyStatus, APPLY_STATUS_SUSPENDED);
                }

                // Check if discount_rt > 10% AND apply_status != "暂停申购"
                var result = meetsDiscountCriteria && meetsApplyCriteria;
                if (result)
                {
                    logger.LogInformation("Fund {FundId} ({FundNm}) ACCEPTED: discount_rt={Discount}%, apply_status='{Status}'", 
                        fund.FundId, fund.FundNm, discountValue, fund.ApplyStatus);
                }
                return result;
            }).ToList();

            logger.LogInformation("Total funds fetched: {Total}, Filtered: {Filtered} (discount_rt > {MinDiscount}% and apply_status != '{SuspendedStatus}')", 
                allFunds.Count, filteredFunds.Count, MIN_DISCOUNT_RATE, APPLY_STATUS_SUSPENDED);

            // Convert to LofArbitrageData format
            var fetchDateTime = GetShanghaiTime();
            var currentDate = fetchDateTime.ToString("yyyy-MM-dd");
            var lofData = filteredFunds.Select(fund => new LofArbitrageData
            {
                LofFund = fund.FundId,
                Name = fund.FundNm,
                ApplyStatus = fund.ApplyStatus,
                Amount = ConvertApplyStatusToAmount(fund.ApplyStatus, fund.FundId),
                PurchaseDate = currentDate
            }).ToList();

            // Update data store with datetime even if the list is empty (to indicate fetch completed)
            dataStore.UpdateLofData(lofData, fetchDateTime);
            
            if (lofData.Count == 0)
            {
                logger.LogWarning("No funds matched the filtering criteria. Empty list stored.");
            }
            else
            {
                logger.LogInformation("Data fetched successfully at {Time}, {Count} items stored", GetShanghaiTime(), lofData.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch data from jisilu.cn");
        }
    }

    public static DateTime GetShanghaiTime()
    {
        try
        {
            var shanghaiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, shanghaiTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback: China is UTC+8
            return DateTime.UtcNow.AddHours(8);
        }
    }

    private decimal ConvertApplyStatusToAmount(string applyStatus, string fundId)
    {
        if (string.IsNullOrEmpty(applyStatus))
        {
            return UNLIMITED_AMOUNT;
        }

        // Handle "开放申购" (open for subscription) - means unlimited
        // Per requirements: set to 10000 for unlimited status
        // Check for exact match or starts with "开放" to avoid false positives
        if (applyStatus == "开放申购" || applyStatus.StartsWith("开放"))
        {
            return UNLIMITED_AMOUNT;
        }

        try
        {
            // Try to match patterns like "限10", "限1万", "限1千"
            var match = ApplyStatusWithUnitRegex.Match(applyStatus);
            
            if (match.Success)
            {
                var numberStr = match.Groups[1].Value;
                var unit = match.Groups[2].Value; // Empty string when no unit is present
                
                if (decimal.TryParse(numberStr, out var number))
                {
                    // Apply multiplier based on unit
                    if (unit == "万")
                    {
                        return number * 10000m; // 1万 = 10,000 yuan
                    }
                    else if (unit == "千")
                    {
                        return number * 1000m; // 1千 = 1,000 yuan
                    }
                    else
                    {
                        // No unit or empty string means the number is in yuan (e.g., "限10" = 10 yuan)
                        return number;
                    }
                }
            }
            
            // If primary pattern doesn't match, return unlimited amount
            // Do not use fallback regex as it may match unrelated numbers
        }
        catch (Exception ex)
        {
            // Log parsing failures at warning level for visibility
            logger.LogWarning(ex, "Failed to parse ApplyStatus '{ApplyStatus}' for fund {FundId}, defaulting to {Amount}", 
                applyStatus, fundId, UNLIMITED_AMOUNT);
        }

        // Default to 10000 for unlimited/unknown cases per requirements
        return UNLIMITED_AMOUNT;
    }

    [GeneratedRegex(@"限(\d+(?:\.\d+)?)(万|千)?", RegexOptions.Compiled)]
    private static partial Regex InvestAmountRegex();
}
