using System;
using System.IO;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Services;
using ReactiveUI;

namespace Client.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string _logContent = string.Empty;
    private bool _showLogContent = false;
    private string _logStatusMessage = string.Empty;
    private bool _showLogStatus = false;
    private readonly LoggingService _logger;

    public SettingsViewModel()
    {
        _logger = LoggingService.Instance;
        
        ViewLogsCommand = ReactiveCommand.CreateFromTask(ViewLogsAsync);
        RefreshLogsCommand = ReactiveCommand.CreateFromTask(RefreshLogsAsync);
        ClearLogsCommand = ReactiveCommand.CreateFromTask(ClearLogsAsync);
        ShareLogsCommand = ReactiveCommand.CreateFromTask(ShareLogsAsync);
        
        LoadAppInfo();
    }

    public string LogContent
    {
        get => _logContent;
        set => this.RaiseAndSetIfChanged(ref _logContent, value);
    }

    public bool ShowLogContent
    {
        get => _showLogContent;
        set => this.RaiseAndSetIfChanged(ref _showLogContent, value);
    }

    public string LogStatusMessage
    {
        get => _logStatusMessage;
        set => this.RaiseAndSetIfChanged(ref _logStatusMessage, value);
    }

    public bool ShowLogStatus
    {
        get => _showLogStatus;
        set => this.RaiseAndSetIfChanged(ref _showLogStatus, value);
    }

    public string LogDirectoryPath => _logger.LogDirectory;
    
    public bool ShowLogPath => !OperatingSystem.IsAndroid();
    
    public bool CanShareLogs => OperatingSystem.IsAndroid();

    public string AppVersion { get; private set; } = "Version 1.0.0";
    
    public string PlatformInfo { get; private set; } = "Platform: Unknown";

    public ICommand ViewLogsCommand { get; }
    public ICommand RefreshLogsCommand { get; }
    public ICommand ClearLogsCommand { get; }
    public ICommand ShareLogsCommand { get; }

    private void LoadAppInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            AppVersion = $"Version: {version?.ToString() ?? "1.0.0"}";

            if (OperatingSystem.IsAndroid())
            {
                PlatformInfo = "Platform: Android";
            }
            else if (OperatingSystem.IsWindows())
            {
                PlatformInfo = "Platform: Windows";
            }
            else if (OperatingSystem.IsLinux())
            {
                PlatformInfo = "Platform: Linux";
            }
            else if (OperatingSystem.IsMacOS())
            {
                PlatformInfo = "Platform: macOS";
            }
        }
        catch
        {
            // Keep defaults
        }
    }

    private async Task ViewLogsAsync()
    {
        try
        {
            _logger.LogInfo("User requested to view logs");
            
            await Task.Run(() =>
            {
                LogContent = _logger.GetRecentLogs(500);
            });
            
            ShowLogContent = true;
            LogStatusMessage = "Logs loaded successfully";
            ShowLogStatus = true;
            
            // Hide status message after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                ShowLogStatus = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to view logs", ex);
            LogStatusMessage = $"Error loading logs: {ex.Message}";
            ShowLogStatus = true;
        }
    }

    private async Task RefreshLogsAsync()
    {
        if (ShowLogContent)
        {
            await ViewLogsAsync();
        }
    }

    private async Task ClearLogsAsync()
    {
        try
        {
            _logger.LogInfo("User requested to clear logs");
            
            await Task.Run(() =>
            {
                _logger.ClearLogs();
            });
            
            LogContent = string.Empty;
            ShowLogContent = false;
            LogStatusMessage = "Logs cleared successfully";
            ShowLogStatus = true;
            
            // Hide status message after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                ShowLogStatus = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to clear logs", ex);
            LogStatusMessage = $"Error clearing logs: {ex.Message}";
            ShowLogStatus = true;
        }
    }

    private async Task ShareLogsAsync()
    {
        try
        {
            _logger.LogInfo("User requested to share logs");
            
#if ANDROID
            if (OperatingSystem.IsAndroid())
            {
                await Task.Run(() =>
                {
                    // Use app's external files directory (no permissions needed on Android 10+)
                    var context = Android.App.Application.Context;
                    var extFilesDir = context.GetExternalFilesDir(null);
                    if (extFilesDir != null)
                    {
                        var exportPath = Path.Combine(extFilesDir.AbsolutePath, "HBArbitrageRobot_logs.txt");
                        if (_logger.ExportLogs(exportPath))
                        {
                            LogStatusMessage = $"Logs exported successfully!\nLocation: {exportPath}";
                            _logger.LogInfo($"Logs exported to: {exportPath}");
                        }
                        else
                        {
                            LogStatusMessage = "Failed to export logs";
                        }
                    }
                    else
                    {
                        LogStatusMessage = "Cannot access external storage";
                        _logger.LogError("External storage not available");
                    }
                });
            }
            else
#endif
            {
                // On desktop, save to desktop or documents folder
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var exportPath = Path.Combine(desktopPath, $"HBArbitrageRobot_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                
                await Task.Run(() =>
                {
                    if (_logger.ExportLogs(exportPath))
                    {
                        LogStatusMessage = $"Logs exported to: {exportPath}";
                        _logger.LogInfo($"Logs exported to: {exportPath}");
                    }
                    else
                    {
                        LogStatusMessage = "Failed to export logs";
                    }
                });
            }
            
            ShowLogStatus = true;
            
            // Hide status message after 5 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                ShowLogStatus = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to share logs", ex);
            LogStatusMessage = $"Error sharing logs: {ex.Message}";
            ShowLogStatus = true;
        }
    }
}
