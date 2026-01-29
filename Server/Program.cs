using Server.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// In .NET 10, path bases in ASPNETCORE_URLS are not allowed
// Extract any path base from URLs and configure it later with UsePathBase()
string? pathBase = null;
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (!string.IsNullOrEmpty(urls))
{
    var urlParts = urls.Split(';');
    var updatedUrls = new List<string>();
    
    foreach (var url in urlParts)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            {
                // Extract path base from first URL
                if (pathBase == null)
                {
                    pathBase = uri.AbsolutePath;
                }
                // Remove path from URL
                var urlWithoutPath = $"{uri.Scheme}://{uri.Authority}";
                updatedUrls.Add(urlWithoutPath);
            }
            else
            {
                updatedUrls.Add(url);
            }
        }
        else
        {
            updatedUrls.Add(url);
        }
    }
    
    // Update URLs without path bases
    if (pathBase != null)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", string.Join(";", updatedUrls));
        builder.WebHost.UseUrls(updatedUrls.ToArray());
    }
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

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
