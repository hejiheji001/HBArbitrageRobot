using ReactiveUI;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Client.Models;
using Client.Services;

namespace Client.ViewModels;

public class ExecutionViewModel : ViewModelBase
{
    private readonly CredentialStorageService _credentialStorage;
    private readonly ConfigurationService _configurationService;
    private MultiUserExecutionService? _executionService;
    private ConsoleRedirectWriter? _consoleRedirectWriter;
    private ConsoleRedirectWriter? _consoleErrorRedirectWriter;
    
    private bool _isExecuting;
    private string _apiUrl;
    private string _purchaseTime;
    private decimal _maxInvestment;
    private string _statusMessage = "Ready to execute";
    private ApiUrlOption _selectedApiUrlOption;

    public ExecutionViewModel(CredentialStorageService credentialStorage)
    {
        _credentialStorage = credentialStorage;
        _configurationService = new ConfigurationService();
        _configurationService.LoadConfiguration([]);

        var configuredApiUrl = _configurationService.RemoteApiUrl;
        var localApiUrl = _configurationService.LocalApiUrl;
        _purchaseTime = _configurationService.PurchaseTime;
        _maxInvestment = _configurationService.MaxInvestmentPerHolder;

        ApiUrlOptions =
        [
            new($"Local ({localApiUrl})", localApiUrl),
            new($"Remote ({configuredApiUrl})", configuredApiUrl)
        ];

        _selectedApiUrlOption = ResolveApiUrlOption(configuredApiUrl, localApiUrl);
        _apiUrl = _selectedApiUrlOption.Value;

        LogMessages = [];
        SetupConsoleLogRedirection();
        
        StartExecutionCommand = ReactiveCommand.Create(StartExecution);
    }

    public ObservableCollection<string> LogMessages { get; }

    public bool IsExecuting
    {
        get => _isExecuting;
        set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
    }

    public string ApiUrl
    {
        get => _apiUrl;
        set => this.RaiseAndSetIfChanged(ref _apiUrl, value);
    }

    public IReadOnlyList<ApiUrlOption> ApiUrlOptions { get; }

    public ApiUrlOption SelectedApiUrlOption
    {
        get => _selectedApiUrlOption;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedApiUrlOption, value);
            ApiUrl = value.Value;
        }
    }

    public string PurchaseTime
    {
        get => _purchaseTime;
        set => this.RaiseAndSetIfChanged(ref _purchaseTime, value);
    }

    public decimal MaxInvestment
    {
        get => _maxInvestment;
        set => this.RaiseAndSetIfChanged(ref _maxInvestment, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ICommand StartExecutionCommand { get; }

    private void SetupConsoleLogRedirection()
    {
        try
        {
            if (Console.Out is ConsoleRedirectWriter existingWriter)
            {
                existingWriter.UpdateCallback(AddLog);
                _consoleRedirectWriter = existingWriter;
                Console.SetError(existingWriter);
                return;
            }

            var currentOut = Console.Out;
            _consoleRedirectWriter = new ConsoleRedirectWriter(currentOut, AddLog);
            Console.SetOut(_consoleRedirectWriter);

            var currentError = Console.Error;
            _consoleErrorRedirectWriter = new ConsoleRedirectWriter(currentError, AddLog);
            Console.SetError(_consoleErrorRedirectWriter);
        }
        catch (Exception ex)
        {
            AddLog($"Console redirection failed: {ex.Message}");
        }
    }

    private async void StartExecution()
    {
        if (IsExecuting) return;

        // Check if running on Android - browser automation is not supported on Android
        if (OperatingSystem.IsAndroid())
        {
            AddLog("=== Android Platform Detected ===");
            AddLog("");
            AddLog("Browser automation (Playwright) is not supported on Android.");
            AddLog("The Android app is designed for monitoring and user management only.");
            AddLog("");
            AddLog("To execute arbitrage trades, please:");
            AddLog("1. Deploy the Server and Client to AWS Lambda (see DEPLOYMENT_AWS.md)");
            AddLog("2. Use this Android app to monitor execution status");
            AddLog("3. Or run the desktop version of this app on Windows/Linux/Mac");
            AddLog("");
            AddLog("For more information, see:");
            AddLog("- DEPLOYMENT_ANDROID.md");
            AddLog("- DEPLOYMENT_AWS.md");
            return;
        }

        IsExecuting = true;
        
        // Clear log messages on UI thread - same pattern as AddLog for consistency
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            LogMessages.Clear();
        }
        else
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => LogMessages.Clear());
        }
        
        AddLog("Starting execution...");

        try
        {
            _configurationService.SetApiUrlOverride(ApiUrl);
            var apiUrl = await _configurationService.GetApiUrlAsync();
            ApiUrl = apiUrl;

            _executionService = new MultiUserExecutionService(
                _credentialStorage,
                apiUrl,
                _configurationService.ApiToken,
                PurchaseTime,
                MaxInvestment);

            _executionService.ProgressUpdated += OnProgressUpdated;
            _executionService.UserExecutionCompleted += OnUserExecutionCompleted;

            var results = await _executionService.ExecuteForAllUsersAsync();

            var successCount = results.Count(r => r.Success);
            
            // Update status message on UI thread, await to ensure order with subsequent logs
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"Execution completed: {successCount}/{results.Count} successful";
            });
            
            AddLog($"\n=== Execution Complete ===");
            AddLog($"Total users: {results.Count}");
            AddLog($"Successful: {successCount}");
            AddLog($"Failed: {results.Count - successCount}");
        }
        catch (Exception ex)
        {
            // Update status message on UI thread, await to ensure order with subsequent logs
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"Error: {ex.Message}";
            });
            
            AddLog($"ERROR: {ex.Message}");
        }
        finally
        {
            // MUST await to prevent race condition if user clicks button again immediately
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsExecuting = false;
            });
            
            if (_executionService != null)
            {
                _executionService.ProgressUpdated -= OnProgressUpdated;
                _executionService.UserExecutionCompleted -= OnUserExecutionCompleted;
            }
        }
    }

    private void OnProgressUpdated(object? sender, ExecutionProgressEventArgs e)
    {
        // Run on UI thread to avoid cross-thread access violations
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AddLog(e.Message);
            StatusMessage = e.Message;
        });
    }

    private void OnUserExecutionCompleted(object? sender, ExecutionResult e)
    {
        var status = e.Success ? "✅" : "❌";
        var message = $"{status} {e.Username}: {(e.Success ? "Success" : e.ErrorMessage)}";
        
        // Run on UI thread to avoid cross-thread access violations
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AddLog(message);
        });
    }

    private void AddLog(string message)
    {
        var timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        
        // Ensure we're on the UI thread before modifying the collection
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread, add directly
            AddLogMessageToCollection(timestampedMessage);
        }
        else
        {
            // Not on UI thread, dispatch to UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() => AddLogMessageToCollection(timestampedMessage));
        }
    }

    /// <summary>
    /// Adds a timestamped message to the log collection.
    /// MUST be called from the UI thread.
    /// </summary>
    private void AddLogMessageToCollection(string timestampedMessage)
    {
        // Verify we're on the UI thread - throws if not
        Avalonia.Threading.Dispatcher.UIThread.VerifyAccess();
        
        LogMessages.Add(timestampedMessage);
        // Keep only last 1000 messages
        while (LogMessages.Count > 1000)
        {
            LogMessages.RemoveAt(0);
        }
    }

    private ApiUrlOption ResolveApiUrlOption(string? remoteApiUrl, string? localApiUrl)
    {
        if (!string.IsNullOrWhiteSpace(remoteApiUrl))
        {
            return ApiUrlOptions[1];
        }

        if (!string.IsNullOrWhiteSpace(localApiUrl))
        {
            return ApiUrlOptions[0];
        }

        return ApiUrlOptions[1];
    }
}

public record ApiUrlOption(string Name, string Value);
