using Client.Models;
using Client.Services;

internal partial class Program
{
    private static readonly IErrorNotifier _errorNotifier = new ConsoleErrorNotifier();
    private static readonly ConfigurationService _config = new();
    private static readonly ApiClientService _apiClient = new();
    private static readonly SchedulingService _scheduler = new();
    
    // Services that depend on configuration - initialized after config is loaded
    private static BrowserService? _browserService;
    private static AuthenticationService? _authService;
    private static ArbitrageService? _arbitrageService;
    private static WorkflowOrchestrator? _orchestrator;

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("HBArbitrageRobot Client Starting...");
        Console.WriteLine("Running in continuous mode - will check for arbitrage opportunities periodically");

        // Load configuration
        _config.LoadConfiguration(args);
        
        // Initialize services that depend on configuration
        InitializeServices();

        try
        {
            // Main infinite loop
            while (true)
            {
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine($"Starting arbitrage cycle at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine(new string('=', 80));
                
                try
                {
                    await RunArbitrageCycleAsync();
                }
                catch (Exception ex)
                {
                    await _errorNotifier.NotifyAsync("Error during arbitrage cycle", ex);
                    Console.WriteLine("Will retry in the next cycle...");
                }
                
                // Enter standby mode and wait until 14:20
                await _scheduler.EnterStandbyModeAsync();
            }
        }
        catch (Exception ex)
        {
            await _errorNotifier.NotifyAsync("Fatal error in main loop", ex);
            Environment.Exit(1);
        }
    }

    private static void InitializeServices()
    {
        _browserService = new BrowserService();
        _authService = new AuthenticationService(_config.Username, _config.Password);
        _arbitrageService = new ArbitrageService(_authService);
        _orchestrator = new WorkflowOrchestrator(_config, _apiClient, _browserService, _authService, _arbitrageService);
    }

    private static async Task RunArbitrageCycleAsync()
    {
        try
        {
            // Step 1: Fetch LOF arbitrage data from Server API
            Console.WriteLine($"\nFetching LOF arbitrage data from API: {_config.ApiUrl}/api/data");

            var (lofData, shouldWait) = await _apiClient.FetchLofDataFromApiAsync(_config.ApiUrl);

            // Check if server indicated to wait
            if (shouldWait)
            {
                Console.WriteLine("\n⏸️  Server indicated to wait. No arbitrage opportunities available now.");
                Console.WriteLine("   Entering standby mode...");
                return;
            }

            if (lofData == null || lofData.Count == 0)
            {
                Console.WriteLine("ℹ️  No LOF data available from API. Entering standby mode...");
                return;
            }

            Console.WriteLine($"✓ Fetched {lofData.Count} LOF arbitrage items");

            // Step 2: Validate credentials
            ValidateCredentials();

            // Step 3: Run arbitrage workflow
            await ExecuteArbitrageWorkflowAsync(lofData);

            Console.WriteLine($"\n✓ Workflow completed successfully. Waiting {_config.RetryDelaySeconds} seconds before next run...");
            await Task.Delay(TimeSpan.FromSeconds(_config.RetryDelaySeconds));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"\nRecovering from error. Waiting {_config.RetryDelaySeconds} seconds before retry...");
            await Task.Delay(TimeSpan.FromSeconds(_config.RetryDelaySeconds));
        }
    }

    private static void ValidateCredentials()
    {
        if (string.IsNullOrEmpty(_config.Username) || string.IsNullOrEmpty(_config.Password))
        {
            Console.WriteLine("ERROR: Username and password are required.");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Testing: dotnet run <username> <password> [api_url] [pwd] [purchase_time] [max_investment] [retry_delay_seconds]");
            Console.WriteLine("  Production: Set USERNAME, PASSWORD, PWD, PURCHASE_TIME, MAX_INVESTMENT, RETRY_DELAY_SECONDS environment variables");
            throw new InvalidOperationException("Username and password are required");
        }
    }

    private static async Task ExecuteArbitrageWorkflowAsync(List<LofArbitrageData> lofData)
    {
        if (_orchestrator == null)
        {
            throw new InvalidOperationException("Services not initialized. Call InitializeServices first.");
        }
        
        await _orchestrator.RunArbitrageWorkflowAsync(lofData);
    }
}
