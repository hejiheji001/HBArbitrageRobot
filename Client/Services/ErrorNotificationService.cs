namespace Client.Services;

/// <summary>
/// Interface for error notification - can be extended to webhook integration
/// </summary>
public interface IErrorNotifier
{
    Task NotifyAsync(string message, Exception? exception = null);
}

/// <summary>
/// Console implementation of error notifier
/// </summary>
public class ConsoleErrorNotifier : IErrorNotifier
{
    public Task NotifyAsync(string message, Exception? exception = null)
    {
        Console.WriteLine($"\n❌ ERROR: {message}");
        if (exception != null)
        {
            Console.WriteLine($"   Exception: {exception.Message}");
            Console.WriteLine($"   Stack trace: {exception.StackTrace}");
        }
        // TODO: Integrate webhook notification here when needed
        return Task.CompletedTask;
    }
}
