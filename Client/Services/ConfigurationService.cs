using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace Client.Services;

/// <summary>
/// Service for handling application configuration from environment variables and command-line arguments
/// </summary>
public class ConfigurationService
{
    private static readonly Lazy<AmazonSimpleSystemsManagementClient> _ssmClient = new(() =>
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-northeast-1";
        return new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.GetBySystemName(region));
    });

    private const string RemoteApiUrlParameterName = "/hbarbitrage/api-gateway-url";
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string? ApiUrl { get; private set; }
    public string RemoteApiUrl { get; private set; } = string.Empty;
    public string LocalApiUrl { get; private set; } = string.Empty;
    public string ApiToken { get; private set; } = string.Empty;
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
        RemoteApiUrl = GetConfig(args, "API_URL", 2, "");
        ApiUrl = RemoteApiUrl;

        LocalApiUrl = GetConfig(args, "LOCAL_API_URL", 8, string.Empty);

        ApiToken = GetConfig(args, "API_TOKEN", 7, string.Empty);
        
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
    /// Get API URL, falling back to default if not configured
    /// </summary>
    public async Task<string> GetApiUrlAsync()
    {
        if (!string.IsNullOrEmpty(ApiUrl))
        {
            return ApiUrl;
        }

        if (!string.IsNullOrEmpty(RemoteApiUrl))
        {
            ApiUrl = RemoteApiUrl;
            return ApiUrl;
        }

        var remoteFromSsm = await GetRemoteApiUrlFromSsmAsync();
        if (!string.IsNullOrWhiteSpace(remoteFromSsm))
        {
            RemoteApiUrl = remoteFromSsm;
            ApiUrl = remoteFromSsm;
            return ApiUrl;
        }

        if (!string.IsNullOrEmpty(LocalApiUrl))
        {
            ApiUrl = LocalApiUrl;
            return ApiUrl;
        }

        Console.WriteLine("⚠️  API_URL/LOCAL_API_URL not configured.");
        return ApiUrl;
    }

    private static async Task<string?> GetRemoteApiUrlFromSsmAsync()
    {
        try
        {
            var ssmClient = _ssmClient.Value;
            var response = await ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = RemoteApiUrlParameterName
            });

            var url = response.Parameter.Value;
            Console.WriteLine($"✅ Retrieved API Gateway URL from SSM: {url}");
            return url;
        }
        catch (ParameterNotFoundException)
        {
            Console.WriteLine($"⚠️  API Gateway URL not found in SSM Parameter Store: {RemoteApiUrlParameterName}");
            return null;
        }
        catch (Amazon.Runtime.AmazonServiceException ex) when (ex.ErrorCode == "AccessDeniedException")
        {
            Console.WriteLine("⚠️  Access denied to SSM Parameter Store. Ensure ssm:GetParameter permission.");
            return null;
        }
        catch (Amazon.Runtime.AmazonClientException ex)
        {
            Console.WriteLine($"⚠️  AWS client error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Unexpected error retrieving API Gateway URL from SSM: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Update API URL from UI or CLI overrides
    /// </summary>
    public void SetApiUrlOverride(string? apiUrl)
    {
        ApiUrl = string.IsNullOrWhiteSpace(apiUrl) ? string.Empty : apiUrl.Trim();
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
