using Server.Models;

namespace Server.Services;

public class DataFetchService : BackgroundService
{
    private readonly ILogger<DataFetchService> _logger;
    private readonly DataStore _dataStore;
    private readonly HttpClient _httpClient;

    public DataFetchService(
        ILogger<DataFetchService> logger,
        DataStore dataStore,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _dataStore = dataStore;
        _httpClient = httpClientFactory.CreateClient();
        
        // Set user agent and headers to mimic a browser request
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Fetch Service started");

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
            _logger.LogInformation("Next fetch scheduled at: {TargetTime} (in {Delay})", targetTime, delay);

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
                
                _logger.LogInformation("Next fetch scheduled at: {TargetTime} (in {Delay})", nextTargetTime, nextDelay);
                await Task.Delay(nextDelay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Data fetch service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in data fetch loop");
                // Wait 1 minute before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task FetchDataAsync(CancellationToken cancellationToken)
    {
        await FetchDataInternalAsync(cancellationToken);
    }

    public async Task FetchDataInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching data from jisilu.cn at {Time}", GetShanghaiTime());

            const string baseUrl = "https://www.jisilu.cn/data";
            var lofUrls = new[]
            {
                "/lof/index_lof_list/?only_owned=&rp=25",
                "/lof/stock_lof_list/?only_owned=&rp=25",
                "/qdii/qdii_list/E?only_lof=y&rp=22",
                "/qdii/qdii_list/C?rp=22"
            };

            var allFunds = new List<JisiluCell>();

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

                    _logger.LogInformation("Fetching from: {Url}", fullUrl);

                    var response = await _httpClient.GetAsync(fullUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var jisiluResponse = System.Text.Json.JsonSerializer.Deserialize<JisiluResponse>(content);

                    if (jisiluResponse?.Rows != null)
                    {
                        _logger.LogInformation("Received {Count} rows from {Url}", jisiluResponse.Rows.Count, lofUrl);
                        allFunds.AddRange(jisiluResponse.Rows.Select(r => r.Cell));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch data from {Url}", lofUrl);
                    // Continue with other URLs even if one fails
                }
            }

            // Filter funds: discount_rt > 10% AND redeem_status = "开放赎回"
            var filteredFunds = allFunds.Where(fund =>
            {
                // Parse discount_rt (e.g., "41.74%" -> 41.74)
                if (string.IsNullOrEmpty(fund.DiscountRt) || fund.DiscountRt == "-")
                    return false;

                var discountStr = fund.DiscountRt.Replace("%", "").Trim();
                if (!decimal.TryParse(discountStr, out var discountValue))
                    return false;

                // Check if discount_rt > 10% AND redeem_status = "开放赎回"
                return discountValue > 10m && fund.RedeemStatus == "开放赎回";
            }).ToList();

            _logger.LogInformation("Filtered {Count} funds with discount_rt > 10% and redeem_status = '开放赎回'", filteredFunds.Count);

            // Convert to LofArbitrageData format
            var currentDate = GetShanghaiTime().ToString("yyyy-MM-dd");
            var lofData = filteredFunds.Select(fund => new LofArbitrageData
            {
                LofFund = fund.FundId,
                LofArbitrage = new LofArbitrage
                {
                    PurchaseQuantity = "1",
                    PurchaseDate = currentDate
                },
                LofArbitrageStrategy = new LofArbitrageStrategy
                {
                    PolicyType = 4,
                    SellTime = "",
                    SellStockPrice = "",
                    TurnBack = ""
                }
            }).ToList();

            _dataStore.UpdateLofData(lofData);
            _logger.LogInformation("Data fetched successfully at {Time}, {Count} items stored", GetShanghaiTime(), lofData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from jisilu.cn");
        }
    }

    private static DateTime GetShanghaiTime()
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
}
