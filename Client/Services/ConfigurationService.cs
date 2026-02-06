using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace Client.Services;

/// <summary>
/// Service for handling application configuration from environment variables and command-line arguments
/// </summary>
public class ConfigurationService
{
    private static AmazonSimpleSystemsManagementClient? _ssmClient;
    private static readonly object _ssmClientLock = new();
    
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string ApiUrl { get; private set; } = "http://localhost:5000";
    public string PurchaseTime { get; private set; } = "14:15";
    public decimal MaxInvestmentPerHolder { get; private set; } = 1000m;
    public int RetryDelaySeconds { get; private set; } = 60;
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Load configuration from command-line arguments and environment variables
    /// </summary>
    public void LoadConfiguration(string[] args)
    {
        Username = GetConfig(args, "USERNAME", 0);
        Password = GetConfig(args, "PASSWORD", 1);
        
        // Get API URL from config
        var apiUrlConfig = GetConfig(args, "API_URL", 2, "");
        if (!string.IsNullOrEmpty(apiUrlConfig))
        {
            ApiUrl = apiUrlConfig;
        }
        else
        {
            // API URL will be retrieved from SSM on first use if needed
            // This avoids blocking synchronous calls in LoadConfiguration
            ApiUrl = string.Empty;
        }
        
        PurchaseTime = GetConfig(args, "PURCHASE_TIME", 3, "14:15");
        Headless = bool.Parse(GetConfig(args, "HEADLESS", 5, "true"));

        // Parse max investment with error handling
        try
        {
            var maxInvestmentConfig = GetConfig(args, "MAX_INVESTMENT", 4, "1000");
            MaxInvestmentPerHolder = decimal.Parse(maxInvestmentConfig);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse MAX_INVESTMENT, using default of 1000 yuan. Error: {ex.Message}");
        }

        // Parse retry delay with error handling
        try
        {
            var retryDelayConfig = GetConfig(args, "RETRY_DELAY_SECONDS", 6, "60");
            RetryDelaySeconds = int.Parse(retryDelayConfig);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse RETRY_DELAY_SECONDS, using default of 60 seconds. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get API URL, retrieving from SSM if not already configured
    /// </summary>
    public async Task<string> GetApiUrlAsync()
    {
        if (!string.IsNullOrEmpty(ApiUrl))
        {
            return ApiUrl;
        }

        // Retrieve from SSM if not configured
        ApiUrl = await GetApiUrlFromSSM();
        return ApiUrl;
    }

    /// <summary>
    /// Get or create the SSM client singleton
    /// </summary>
    private static AmazonSimpleSystemsManagementClient GetSsmClient()
    {
        if (_ssmClient == null)
        {
            lock (_ssmClientLock)
            {
                if (_ssmClient == null)
                {
                    var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-northeast-1";
                    _ssmClient = new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.GetBySystemName(region));
                }
            }
        }
        return _ssmClient;
    }

    /// <summary>
    /// Retrieve Lambda function URL from AWS SSM Parameter Store
    /// </summary>
    private static async Task<string> GetApiUrlFromSSM()
    {
        try
        {
            var ssmClient = GetSsmClient();
            
            var request = new GetParameterRequest
            {
                Name = "/hbarbitrage/lambda/server-url"
            };
            
            var response = await ssmClient.GetParameterAsync(request);
            var url = response.Parameter.Value;
            
            Console.WriteLine($"✅ Retrieved Lambda function URL from SSM: {url}");
            return url;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to retrieve Lambda function URL from SSM: {ex.Message}");
            Console.WriteLine("Using default URL: http://localhost:5000");
            return "http://localhost:5000";
        }
    }

    /// <summary>
    /// Get configuration value from command-line args or environment variables
    /// </summary>
    private static string GetConfig(string[] args, string envVarName, int argIndex, string? defaultValue = null)
    {
        // First check command line arguments
        if (args.Length > argIndex && !string.IsNullOrEmpty(args[argIndex]))
        {
            return args[argIndex];
        }

        // Then check environment variables
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue))
        {
            return envValue;
        }

        // Return default value if provided
        return defaultValue ?? string.Empty;
    }
}
