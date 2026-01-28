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
        try
        {
            _logger.LogInformation("Fetching data from example.com at {Time}", GetShanghaiTime());

            var response = await _httpClient.GetAsync("http://example.com", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Parse the content as JSON array of LOF arbitrage data
            List<LofArbitrageData> lofData;
            try
            {
                lofData = System.Text.Json.JsonSerializer.Deserialize<List<LofArbitrageData>>(content) ?? new List<LofArbitrageData>();
                
                // Set purchaseDate to current date in Shanghai timezone for all items
                var currentDate = GetShanghaiTime().ToString("yyyy-MM-dd");
                foreach (var item in lofData)
                {
                    if (item.LofArbitrage != null)
                    {
                        item.LofArbitrage.PurchaseDate = currentDate;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse content as LOF data, creating default entry");
                // If parsing fails, create a default entry with example data
                lofData = new List<LofArbitrageData>
                {
                    new LofArbitrageData
                    {
                        LofFund = "161226",
                        LofArbitrage = new LofArbitrage
                        {
                            PurchaseQuantity = "1",
                            PurchaseDate = GetShanghaiTime().ToString("yyyy-MM-dd")
                        },
                        LofArbitrageStrategy = new LofArbitrageStrategy
                        {
                            PolicyType = 4,
                            SellTime = "",
                            SellStockPrice = "",
                            TurnBack = ""
                        }
                    }
                };
            }

            _dataStore.UpdateLofData(lofData);
            _logger.LogInformation("Data fetched successfully at {Time}, {Count} items", GetShanghaiTime(), lofData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from example.com");
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
