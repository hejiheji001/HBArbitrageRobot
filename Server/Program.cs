using Server.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Add MemoryCache
builder.Services.AddMemoryCache();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Register our services
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<DataFetchService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DataFetchService>());

var app = builder.Build();

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
