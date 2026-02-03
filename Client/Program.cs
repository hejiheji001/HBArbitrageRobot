using Avalonia;
using System;
using System.Threading.Tasks;
using Client.Services;

namespace Client;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        // Check if CLI mode is requested
        // CLI mode is activated when command-line arguments are provided (username and password)
        // or when explicit --cli flag is used
        if (args.Length > 0 && (args[0] == "--cli" || args[0] == "-c" || !args[0].StartsWith("--")))
        {
            return await RunCliMode(args);
        }

        // Otherwise, run GUI mode
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    private static async Task<int> RunCliMode(string[] args)
    {
        Console.WriteLine("=== HBArbitrageRobot CLI Mode ===");
        Console.WriteLine("Starting Client in CLI mode...\n");

        // Remove --cli flag if present
        if (args.Length > 0 && (args[0] == "--cli" || args[0] == "-c"))
        {
            args = args[1..];
        }

        // Initialize services
        var config = new ConfigurationService();
        config.LoadConfiguration(args);

        // Validate required configuration
        if (string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
        {
            Console.WriteLine("Error: Username and Password are required.");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  dotnet run <username> <password> [api_url] [pwd] [purchase_time] [max_investment]");
            Console.WriteLine("  dotnet run --cli  # Uses environment variables");
            Console.WriteLine("\nOr set environment variables:");
            Console.WriteLine("  USERNAME=your_username");
            Console.WriteLine("  PASSWORD=your_password");
            Console.WriteLine("  PWD=your_transaction_password");
            Console.WriteLine("  PURCHASE_TIME=14:15");
            Console.WriteLine("  API_URL=http://localhost:5000");
            Console.WriteLine("  MAX_INVESTMENT=1000");
            return 1;
        }

        var apiClient = new ApiClientService();
        var browserService = new BrowserService();
        var authService = new AuthenticationService(config.Username, config.Password);
        var arbitrageService = new ArbitrageService(authService);
        var schedulingService = new SchedulingService();
        var orchestrator = new WorkflowOrchestrator(
            config,
            apiClient,
            browserService,
            authService,
            arbitrageService
        );

        // Run continuous arbitrage workflow
        while (true)
        {
            try
            {
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine($"Starting arbitrage cycle at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine(new string('=', 80));

                // Fetch LOF data from server
                var (lofData, shouldWait) = await apiClient.FetchLofDataFromApiAsync(config.ApiUrl);

                if (shouldWait || lofData == null || lofData.Count == 0)
                {
                    Console.WriteLine("\n⚠ No LOF data available from server or server is still fetching data.");
                    Console.WriteLine("Entering standby mode...");
                }
                else
                {
                    Console.WriteLine($"\n✓ Fetched {lofData.Count} LOF items from server");

                    // Run arbitrage workflow with headless browser (CLI mode always uses headless)
                    await orchestrator.RunArbitrageWorkflowAsync(lofData, headless: true);

                    Console.WriteLine("\n✓ Arbitrage workflow completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error during arbitrage workflow: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            // Enter standby mode until next execution time
            await schedulingService.EnterStandbyModeAsync();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
