# Android App Log Collection Guide

This guide explains how to collect and view logs from the HBArbitrageRobot Android app when crashes or issues occur.

## Overview

The HBArbitrageRobot app now includes a comprehensive logging system that:
- ✅ Automatically captures all application events
- ✅ Records crashes and unhandled exceptions
- ✅ Stores logs in accessible files
- ✅ Provides in-app log viewer
- ✅ Allows easy log export and sharing

## Where Are Logs Stored?

### On Android
- **Internal logs** (automatic): `/data/data/com.hbarbitragerobot.client/files/logs/app_YYYYMMDD.log`
- **Exported logs** (via Share Logs button): `/sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt`

### On Windows/Desktop
- **Internal logs**: `%USERPROFILE%\Documents\HBArbitrageRobot\logs\app_YYYYMMDD.log`
- **Exported logs**: `Desktop\HBArbitrageRobot_logs_YYYYMMDD_HHMMSS.txt`

## How to View Logs

### Method 1: Using the App UI (Recommended)

This is the easiest way to view logs directly from your Android device:

1. **Open the app**
   - Launch HBArbitrageRobot on your Android device

2. **Navigate to Settings**
   - Tap on the **Settings** tab at the bottom or in the navigation menu

3. **View Logs**
   - Tap the **View Logs** button
   - The most recent 500 log entries will be displayed in a scrollable text area
   - You can read through the logs to find error messages or crash information

4. **Refresh Logs**
   - Tap **Refresh** to reload the latest logs
   - Useful if the app generated new logs since you last viewed them

5. **Share Logs**
   - Tap **Share Logs** to export all logs to a file
   - The file location will be displayed (typically in the app's external storage)
   - You can then access this file using a file manager app

### Method 2: Using ADB (For Developers)

If you have ADB installed and your device connected:

#### View Real-Time Logs
```bash
# See all app logs in real-time
adb logcat | grep "HBArbitrageRobot"

# See only error logs
adb logcat *:E | grep "HBArbitrageRobot"

# See all system logs (verbose)
adb logcat
```

#### Save Logs to File
```bash
# Save current logs to a file
adb logcat -d > app-logs.txt

# Save only errors
adb logcat -d *:E > app-errors.txt

# Clear existing logs and start fresh
adb logcat -c
adb logcat -d > app-logs-fresh.txt
```

#### Pull Log Files from Device
```bash
# Pull the exported log file (no root needed)
adb pull /sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt ./my-logs.txt

# Pull internal log files (requires debuggable build)
adb shell run-as com.hbarbitragerobot.client cat /data/data/com.hbarbitragerobot.client/files/logs/app_*.log > internal-logs.txt
```

### Method 3: Using File Manager App

On your Android device:

1. Install a file manager app (e.g., Files by Google, Total Commander, etc.)
2. Navigate to: `Android/data/com.hbarbitragerobot.client/files/`
3. Look for `HBArbitrageRobot_logs.txt` (created when you use Share Logs)
4. Open the file with a text editor app
5. You can also share the file via email, messaging apps, cloud storage, etc.

## Understanding Log Entries

Each log entry follows this format:
```
[YYYY-MM-DD HH:mm:ss.fff] [LEVEL] Message
```

### Log Levels
- **INFO**: Normal application events (startup, user actions, etc.)
- **DEBUG**: Detailed diagnostic information
- **WARN**: Warning messages (potential issues)
- **ERROR**: Error messages with stack traces

### Example Log Entries

**Normal startup:**
```
[2026-02-05 04:29:52.123] [INFO] LoggingService initialized
[2026-02-05 04:29:52.456] [INFO] MainActivity OnCreate - App starting
[2026-02-05 04:29:53.789] [INFO] Application started - Environment: Production
```

**Error with exception:**
```
[2026-02-05 14:15:30.123] [ERROR] Failed to connect to API
Exception: System.Net.Http.HttpRequestException: Connection refused
StackTrace:    at System.Net.Http.HttpClient.SendAsync(...)
   at Client.Services.ApiClientService.FetchDataAsync(...)
```

**Crash:**
```
[2026-02-05 14:20:15.678] [ERROR] Unhandled exception occurred
Exception: System.NullReferenceException: Object reference not set to an instance of an object
StackTrace:    at Client.ViewModels.ExecutionViewModel.ExecuteAsync(...)
   at System.Threading.Tasks.Task.Execute(...)
```

## What Gets Logged?

The logging system captures:

✅ **Application Lifecycle**
- App startup and shutdown
- Activity creation (MainActivity OnCreate)
- Configuration loading

✅ **User Actions**
- Button clicks and navigation
- Settings changes
- User credential operations

✅ **Network Operations**
- API calls and responses
- Connection errors
- Server communication

✅ **Browser Automation**
- Playwright browser operations
- Page navigation
- Element interactions

✅ **Errors and Exceptions**
- Unhandled exceptions (crashes)
- Caught exceptions with context
- Full stack traces
- Task exceptions

✅ **System Information**
- Environment variables
- Platform information
- File system operations

## Log Management

### Automatic Log Rotation
- Logs automatically rotate when a file reaches **5MB**
- Up to **5 log files** are kept (approximately 25MB total)
- Oldest log files are automatically deleted
- Log files are named by date: `app_YYYYMMDD.log`

### Manual Log Management

**Clear Logs** (via app UI):
1. Open Settings tab
2. Tap **Clear Logs** button
3. All log files will be deleted
4. A fresh log file will be created

**Note**: Clearing logs is permanent and cannot be undone!

## Troubleshooting Log Collection

### "Cannot view logs"
- Make sure you've used the app at least once to generate logs
- Try tapping **Refresh** to reload
- Check that you have sufficient storage space

### "Cannot export logs"
- Ensure the app has storage permissions
- Check available storage space on your device
- Try exporting to a different location

### "Log file not found via file manager"
1. Make sure you clicked **Share Logs** first
2. Navigate to the correct path: `Android/data/com.hbarbitragerobot.client/files/`
3. Some file managers require special permissions to access this directory
4. Use ADB as an alternative method

### "Empty or missing logs"
- Check if the app is actually running and generating activity
- Look for the current date's log file: `app_YYYYMMDD.log`
- Check if log rotation deleted old files (only keeps last 5)

## Sharing Logs for Support

When reporting crashes or bugs:

1. **Reproduce the issue** if possible
2. **Export logs immediately** using the Share Logs button
3. **Locate the exported file**: 
   - Path shown in app after export
   - Usually: `/sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt`
4. **Share via**:
   - Email: Attach the log file
   - GitHub Issue: Upload as attachment or paste content
   - Cloud storage: Upload and share link
   - File transfer: Use ADB or USB connection

### Example: Sharing via Email

1. Open your email app
2. Compose new email to support
3. Tap attachment icon
4. Navigate to: `Android/data/com.hbarbitragerobot.client/files/`
5. Select `HBArbitrageRobot_logs.txt`
6. Add description of the issue
7. Send

### Example: Creating GitHub Issue

1. Go to: https://github.com/hejiheji001/HBArbitrageRobot/issues
2. Click "New Issue"
3. Describe the problem
4. Attach the log file or paste relevant log entries
5. Submit

## Privacy and Security

### What's in the Logs?
- Application events and operations
- Error messages and stack traces
- File paths and system information
- API URLs (without credentials)

### What's NOT in the Logs?
- ❌ Passwords or credentials
- ❌ Personal financial data
- ❌ Authentication tokens
- ❌ Private user information

### Before Sharing Logs
- Review the log file contents
- Remove any sensitive information if present
- Share only with trusted parties or support channels

## Advanced: Log Analysis

### Finding Specific Issues

**Search for errors:**
```bash
grep "ERROR" HBArbitrageRobot_logs.txt
```

**Filter by date:**
```bash
grep "2026-02-05" HBArbitrageRobot_logs.txt
```

**Find specific exception:**
```bash
grep -A 10 "NullReferenceException" HBArbitrageRobot_logs.txt
```

**Count errors:**
```bash
grep -c "ERROR" HBArbitrageRobot_logs.txt
```

## FAQ

**Q: How long are logs kept?**
A: Logs are kept until they rotate (5 files max, 5MB each). This typically covers several days to weeks depending on app usage.

**Q: Do logs impact app performance?**
A: No, logging is lightweight and uses minimal resources. File I/O is optimized and asynchronous.

**Q: Can I disable logging?**
A: Logging is essential for diagnosing crashes. However, you can clear logs regularly if storage is a concern.

**Q: Where do crash logs go on desktop versions?**
A: Desktop versions log to `Documents/HBArbitrageRobot/logs/` and can be viewed in the Settings tab.

**Q: Can I view logs from previous days?**
A: Yes, using the "View Logs" button shows recent entries, but you can access older log files directly from the log directory.

**Q: How do I know if the app crashed?**
A: Look for log entries with "Unhandled exception" or "ERROR" level messages near the end of the log file.

## Summary

**Quick Steps to Collect Logs:**
1. ✅ Open app → Settings tab
2. ✅ Tap "View Logs" to see recent logs
3. ✅ Tap "Share Logs" to export all logs
4. ✅ Find exported file in: `Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt`
5. ✅ Share with support or analyze yourself

**Alternative via ADB:**
```bash
adb logcat -d > app-logs.txt
adb pull /sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt
```

For more help, see:
- [ANDROID_PHONE_DEBUG.md](ANDROID_PHONE_DEBUG.md) - Full Android debugging guide
- [TESTING.md](TESTING.md) - Testing documentation
- [GitHub Issues](https://github.com/hejiheji001/HBArbitrageRobot/issues) - Report bugs
