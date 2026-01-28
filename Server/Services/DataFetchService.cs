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

            var data = new ArbitrageData
            {
                Content = content,
                FetchedAt = GetShanghaiTime(),
                Source = "example.com"
            };

            _dataStore.UpdateData(data);
            _logger.LogInformation("Data fetched successfully at {Time}", data.FetchedAt);
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
