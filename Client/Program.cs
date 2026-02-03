using Avalonia;
using Client.Services;
using Avalonia.ReactiveUI;

namespace Client;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        // Check environment for development mode
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        
        // Always show console output if in development mode
        if (isDevelopment)
        {
            Console.WriteLine("=== HBArbitrageRobot - Development Mode ===");
            Console.WriteLine($"Environment: {environment}");
            Console.WriteLine($"Args: {string.Join(" ", args)}");
            Console.WriteLine();
        }
        
        // Check if CLI mode is requested
        // CLI mode is activated when command-line arguments are provided (username and password)
        // or when explicit --cli flag is used
        if (args.Length > 0 && (args[0] == "--cli" || args[0] == "-c" || !args[0].StartsWith("--")))
        {
            return await RunCliMode(args);
        }

        // Otherwise, run GUI mode
        if (isDevelopment)
        {
            Console.WriteLine("Starting GUI mode...");
        }
        
        // Load and display existing users before starting GUI
        DisplayExistingUsers();
        
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
        var credentialStorage = new CredentialStorageService();

        // Check if username/password were provided via args or env
        bool credentialsProvided = !string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password);

        if (credentialsProvided)
        {
            // Credentials provided: save to SQLite for future use
            Console.WriteLine($"✓ Credentials provided for user: {config.Username}");
            
            // Check if user already exists
            var existingUsers = credentialStorage.GetAllUsers();
            var existingUser = existingUsers.FirstOrDefault(u => u.Username.Equals(config.Username, StringComparison.OrdinalIgnoreCase));
            
            if (existingUser != null)
            {
                // Update existing user
                existingUser.DecryptedPassword = config.Password;
                existingUser.IsEnabled = true;
                credentialStorage.SaveUser(existingUser);
                Console.WriteLine($"✓ Updated existing user in database: {config.Username}");
            }
            else
            {
                // Create new user
                var newUser = new Models.UserCredential
                {
                    Username = config.Username,
                    DecryptedPassword = config.Password,
                    DisplayName = config.Username,
                    IsEnabled = true,
                    CreatedAt = DateTime.Now
                };
                credentialStorage.SaveUser(newUser);
                Console.WriteLine($"✓ Saved new user to database: {config.Username}");
            }
        }
        else
        {
            // No credentials provided: load from SQLite
            Console.WriteLine("No credentials provided, loading from database...");
            var users = credentialStorage.GetAllUsers().Where(u => u.IsEnabled).ToList();
            
            if (users.Count == 0)
            {
                Console.WriteLine("Error: No enabled users found in database and no credentials provided.");
                Console.WriteLine("\nUsage:");
                Console.WriteLine("  dotnet run <username> <password> [api_url] [purchase_time] [max_investment]");
                Console.WriteLine("  dotnet run --cli  # Uses environment variables or loads from database");
                Console.WriteLine("\nOr set environment variables:");
                Console.WriteLine("  USERNAME=your_username");
                Console.WriteLine("  PASSWORD=your_password");
                Console.WriteLine("  PURCHASE_TIME=14:15");
                Console.WriteLine("  API_URL=http://localhost:5000");
                Console.WriteLine("  MAX_INVESTMENT=1000");
                Console.WriteLine("  HEADLESS=true");
                return 1;
            }

            Console.WriteLine($"\n✓ Loaded {users.Count} enabled user(s) from database:");
            foreach (var user in users)
            {
                var lastRun = user.LastExecutedAt.HasValue 
                    ? user.LastExecutedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                    : "Never";
                Console.WriteLine($"  - {user.Username} (Display: {user.DisplayName ?? "N/A"}, Last run: {lastRun})");
            }
            Console.WriteLine();

            // Execute for all enabled users using MultiUserExecutionService
            return await RunMultiUserCliMode(credentialStorage, config);
        }

        // Single user mode - original behavior
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
                var (lofData, shouldWait) = await ApiClientService.FetchLofDataFromApiAsync(config.ApiUrl);

                if (shouldWait || lofData == null || lofData.Count == 0)
                {
                    Console.WriteLine("\n⚠ No LOF data available from server or server is still fetching data.");
                    Console.WriteLine("Entering standby mode...");
                }
                else
                {
                    Console.WriteLine($"\n✓ Fetched {lofData.Count} LOF items from server");

                    await orchestrator.RunArbitrageWorkflowAsync(lofData);

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

    private static async Task<int> RunMultiUserCliMode(CredentialStorageService credentialStorage, ConfigurationService config)
    {
        var schedulingService = new SchedulingService();
        
        // Run continuous multi-user arbitrage workflow
        while (true)
        {
            try
            {
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine($"Starting multi-user arbitrage cycle at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine(new string('=', 80));

                // Create execution service for all enabled users
                var executionService = new MultiUserExecutionService(
                    credentialStorage,
                    config.ApiUrl,
                    config.PurchaseTime,
                    config.MaxInvestmentPerHolder
                );

                // Execute for all enabled users
                var results = await executionService.ExecuteForAllUsersAsync();

                var successCount = results.Count(r => r.Success);
                Console.WriteLine($"\n✓ Multi-user execution completed: {successCount}/{results.Count} successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error during multi-user arbitrage workflow: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            // Enter standby mode until next execution time
            await schedulingService.EnterStandbyModeAsync();
        }
    }

    private static void DisplayExistingUsers()
    {
        try
        {
            var credentialStorage = new CredentialStorageService();
            var users = credentialStorage.GetAllUsers();
            
            if (users.Count > 0)
            {
                Console.WriteLine("\n=== Existing Users ===");
                foreach (var user in users)
                {
                    var status = user.IsEnabled ? "✓ Enabled" : "✗ Disabled";
                    var lastRun = user.LastExecutedAt.HasValue 
                        ? user.LastExecutedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                        : "Never";
                    Console.WriteLine($"  {status} - {user.Username} (Display: {user.DisplayName ?? "N/A"}, Last run: {lastRun})");
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("\nNo users configured yet. Use the User Management tab to add users.\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load existing users: {ex.Message}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
