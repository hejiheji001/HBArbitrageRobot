using System.Text;

namespace Client.Services;

/// <summary>
/// Centralized logging service for the application with file-based logging support
/// </summary>
public class LoggingService
{
    private static LoggingService? _instance;
    private static readonly object _lock = new object();
    private readonly string _logDirectory;
    private readonly string _currentLogFile = string.Empty;
    private readonly long _maxLogFileSize = 5 * 1024 * 1024; // 5MB
    private readonly int _maxLogFiles = 5;
    private readonly object _fileLock = new object();

    private LoggingService()
    {
        // Determine log directory based on platform
        if (OperatingSystem.IsAndroid())
        {
            // On Android, use the app's private directory
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appData, "logs");
        }
        else
        {
            // On desktop, use user's documents folder
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _logDirectory = Path.Combine(documents, "HBArbitrageRobot", "logs");
        }

        // Create log directory if it doesn't exist
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Set current log file
            var dateStamp = DateTime.Now.ToString("yyyyMMdd");
            _currentLogFile = Path.Combine(_logDirectory, $"app_{dateStamp}.log");

            // Perform log rotation if needed
            RotateLogsIfNeeded();

            // Log initialization
            LogInfo("LoggingService initialized");
            LogInfo($"Log directory: {_logDirectory}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize logging: {ex.Message}");
        }
    }

    public static LoggingService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new LoggingService();
                    }
                }
            }
            return _instance;
        }
    }

    public string LogDirectory => _logDirectory;

    public void LogInfo(string message)
    {
        Log("INFO", message);
    }

    public void LogWarning(string message)
    {
        Log("WARN", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message}\nException: {exception.Message}\nStackTrace: {exception.StackTrace}"
            : message;
        Log("ERROR", fullMessage);
    }

    public void LogDebug(string message)
    {
        Log("DEBUG", message);
    }

    private void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";

        // Always write to console
        Console.WriteLine(logEntry);

        // Write to file
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
                
                // Check if we need to rotate
                var fileInfo = new FileInfo(_currentLogFile);
                if (fileInfo.Exists && fileInfo.Length > _maxLogFileSize)
                {
                    RotateLogsIfNeeded();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }

    private void RotateLogsIfNeeded()
    {
        try
        {
            var logFiles = Directory.GetFiles(_logDirectory, "app_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // If we have more than max files, delete the oldest ones
            while (logFiles.Count >= _maxLogFiles)
            {
                var oldestFile = logFiles.Last();
                try
                {
                    oldestFile.Delete();
                    Console.WriteLine($"Deleted old log file: {oldestFile.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete old log file {oldestFile.Name}: {ex.Message}");
                }
                logFiles.RemoveAt(logFiles.Count - 1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to rotate logs: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all log files in the log directory
    /// </summary>
    public List<string> GetLogFiles()
    {
        try
        {
            return Directory.GetFiles(_logDirectory, "app_*.log")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Gets the content of the most recent log file
    /// </summary>
    public string GetRecentLogs(int maxLines = 500)
    {
        try
        {
            var logFiles = GetLogFiles();
            if (logFiles.Count == 0)
            {
                return "No log files found.";
            }

            var mostRecentLog = logFiles.First();
            var lines = File.ReadAllLines(mostRecentLog);
            var recentLines = lines.TakeLast(maxLines);
            
            return string.Join(Environment.NewLine, recentLines);
        }
        catch (Exception ex)
        {
            return $"Failed to read logs: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets all logs combined from all log files
    /// </summary>
    public string GetAllLogs()
    {
        try
        {
            var sb = new StringBuilder();
            var logFiles = GetLogFiles();
            
            foreach (var logFile in logFiles)
            {
                sb.AppendLine($"=== {Path.GetFileName(logFile)} ===");
                sb.AppendLine(File.ReadAllText(logFile));
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to read all logs: {ex.Message}";
        }
    }

    /// <summary>
    /// Exports logs to a specified file path
    /// </summary>
    public bool ExportLogs(string destinationPath)
    {
        try
        {
            var allLogs = GetAllLogs();
            File.WriteAllText(destinationPath, allLogs);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to export logs: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clears all log files
    /// </summary>
    public void ClearLogs()
    {
        try
        {
            var logFiles = GetLogFiles();
            foreach (var logFile in logFiles)
            {
                try
                {
                    File.Delete(logFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete log file {logFile}: {ex.Message}");
                }
            }
            LogInfo("Log files cleared");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clear logs: {ex.Message}");
        }
    }
}
