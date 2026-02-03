namespace Client.Services;

/// <summary>
/// Service for handling timing and scheduling operations
/// </summary>
public class SchedulingService
{
    /// <summary>
    /// Enter standby mode and wait until 14:20 Shanghai time
    /// </summary>
    public async Task EnterStandbyModeAsync()
    {
        Console.WriteLine("\n" + new string('-', 80));
        Console.WriteLine("Entering standby mode...");
        
        var shanghaiTimeZone = GetShanghaiTimeZone();
        var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, shanghaiTimeZone);
        
        // Target time is 14:20 today
        var targetTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 14, 20, 0);
        
        // If current time is past 14:20, target next day's 14:20
        if (currentTime > targetTime)
        {
            targetTime = targetTime.AddDays(1);
            Console.WriteLine($"Current time ({currentTime:HH:mm:ss}) is past today's 14:20. Will wait until tomorrow.");
        }
        
        var waitDuration = CalculateSafeWaitDuration(targetTime - currentTime);
        
        Console.WriteLine($"Current Shanghai time: {currentTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Next arbitrage check: {targetTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Sleep duration: {waitDuration.Days} days, {waitDuration.Hours} hours, {waitDuration.Minutes} minutes, {waitDuration.Seconds} seconds");
        Console.WriteLine(new string('-', 80));
        
        // Sleep until target time
        await Task.Delay(waitDuration);
        
        Console.WriteLine($"\n⏰ Waking up at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private static TimeZoneInfo GetShanghaiTimeZone()
    {
        try
        {
            // Try Windows timezone ID first
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Try IANA timezone ID (for Linux/Mac)
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback: Create a custom timezone for UTC+8
                Console.WriteLine("Warning: Shanghai timezone not found. Using UTC+8 offset.");
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Shanghai Custom", 
                    TimeSpan.FromHours(8), 
                    "Shanghai Time", 
                    "Shanghai Time");
            }
        }
    }

    private static TimeSpan CalculateSafeWaitDuration(TimeSpan requestedDuration)
    {
        // Validate wait duration to prevent Task.Delay overflow
        // Maximum safe duration is about 24.8 days (int.MaxValue milliseconds)
        var maxSafeDuration = TimeSpan.FromDays(24);
        
        if (requestedDuration > maxSafeDuration || requestedDuration < TimeSpan.Zero)
        {
            Console.WriteLine($"Warning: Calculated wait duration ({requestedDuration}) is invalid. Using 1 hour fallback.");
            return TimeSpan.FromHours(1);
        }
        
        return requestedDuration;
    }
}
