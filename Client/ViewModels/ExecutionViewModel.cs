using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Input;
using Client.Models;
using Client.Services;

namespace Client.ViewModels;

public class ExecutionViewModel : ViewModelBase
{
    private readonly CredentialStorageService _credentialStorage;
    private readonly ApiClientService _apiClient;
    private MultiUserExecutionService? _executionService;
    
    private bool _isExecuting;
    private string _apiUrl;
    private string _purchaseTime;
    private decimal _maxInvestment;
    private string _statusMessage = "Ready to execute";

    public ExecutionViewModel(CredentialStorageService credentialStorage, ApiClientService apiClient)
    {
        _credentialStorage = credentialStorage;
        _apiClient = apiClient;
        
        // Read configuration from environment variables (set by launchSettings.json)
        _apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
        _purchaseTime = Environment.GetEnvironmentVariable("PURCHASE_TIME") ?? "14:15";
        
        // Parse max investment with error handling
        var maxInvestmentStr = Environment.GetEnvironmentVariable("MAX_INVESTMENT");
        if (!string.IsNullOrEmpty(maxInvestmentStr) && decimal.TryParse(maxInvestmentStr, out var maxInv))
        {
            _maxInvestment = maxInv;
        }
        else
        {
            _maxInvestment = 1000m;
        }
        
        LogMessages = new ObservableCollection<string>();
        
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

    private async void StartExecution()
    {
        if (IsExecuting) return;

        IsExecuting = true;
        
        // Clear on UI thread - AddLog() handles its own thread dispatching
        Avalonia.Threading.Dispatcher.UIThread.Post(() => LogMessages.Clear());
        AddLog("Starting execution...");

        try
        {
            _executionService = new MultiUserExecutionService(
                _apiClient,
                _credentialStorage,
                ApiUrl,
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
}
