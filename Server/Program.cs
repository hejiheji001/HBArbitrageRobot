using Server.Services;
using Scalar.AspNetCore;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// In .NET 10, path bases in ASPNETCORE_URLS are not allowed
// Extract any path base from URLs and configure it later with UsePathBase()
string? pathBase = null;
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrEmpty(urls))
{
    var urlList = urls.Split(';');
    var updatedUrls = new List<string>();
    
    foreach (var url in urlList)
    {
        var trimmedUrl = url.Trim();
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            {
                // Extract path base from first URL (all URLs should have the same path base)
                if (pathBase == null)
                {
                    pathBase = uri.AbsolutePath;
                }
                else if (pathBase != uri.AbsolutePath)
                {
                    // Log warning if multiple different path bases are detected
                    Console.WriteLine($"Warning: Multiple different path bases detected. Using '{pathBase}' for all URLs.");
                }
                
                // Reconstruct URL without path using UriBuilder
                var uriBuilder = new UriBuilder(uri)
                {
                    Path = string.Empty
                };
                updatedUrls.Add(uriBuilder.Uri.ToString().TrimEnd('/'));
            }
            else
            {
                updatedUrls.Add(trimmedUrl);
            }
        }
        else
        {
            // Keep non-parseable URLs (like http://+:8080) as-is
            updatedUrls.Add(trimmedUrl);
        }
    }
    
    // Configure Kestrel to use URLs without path bases
    if (pathBase != null)
    {
        builder.WebHost.UseUrls(updatedUrls.ToArray());
    }
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure HttpClient with Polly retry policy
builder.Services.AddHttpClient("DataFetchClient", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler(GetRetryPolicy());

// Add MemoryCache
builder.Services.AddMemoryCache();

// Register our services
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<DataFetchService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DataFetchService>());

var app = builder.Build();

// Configure path base if it was extracted from URLs
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

// Configure the HTTP request pipeline.
// Enable OpenAPI and Scalar in development, or when explicitly enabled in production
var enableApiDocs = app.Environment.IsDevelopment() || 
                    app.Configuration.GetValue("EnableApiDocs", false);

if (enableApiDocs)
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("HBArbitrageRobot API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Only use HTTPS redirection in development, not in Docker
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();

// Polly retry policy for handling transient failures
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // Handles HttpRequestException, 5xx and 408 responses
        .Or<TaskCanceledException>() // Handle TaskCanceledException (including timeouts)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var errorMessage = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString() ?? "Unknown error";
                // Note: Using Console.WriteLine here as ILogger is not available in this static context
                // In production, this will be visible in application logs
                Console.WriteLine($"[Retry Policy] Attempt {retryCount} will retry after {timespan.TotalSeconds}s due to: {errorMessage}");
            });
}
