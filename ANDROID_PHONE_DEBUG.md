# Android Phone Debugging Guide

This guide explains how to deploy and debug the HBArbitrageRobot Android app directly to your Android phone from your Windows development machine.

## Prerequisites

### On Windows
- Visual Studio 2022 (with .NET MAUI/Android workload) OR
- Visual Studio Code with C# extension
- .NET 10 SDK
- Android workload: `dotnet workload install android`
- ADB (Android Debug Bridge) - comes with Android SDK

### On Android Phone
- Android 5.0 or higher
- USB Debugging enabled
- Developer Options unlocked

---

## Step 1: Enable Developer Mode on Android Phone

1. Open **Settings** on your Android phone
2. Scroll to **About Phone**
3. Find **Build Number**
4. Tap **Build Number** 7 times rapidly
5. You'll see a message "You are now a developer!"

## Step 2: Enable USB Debugging

1. Go back to **Settings**
2. Find **Developer Options** (usually under System or Advanced)
3. Enable **Developer Options** toggle
4. Scroll down and enable **USB Debugging**
5. (Optional) Enable **Stay Awake** to prevent screen timeout during debugging

## Step 3: Connect Phone to Windows

1. Connect your Android phone to Windows PC using USB cable
2. On your phone, you'll see a prompt "Allow USB debugging?"
3. Check **Always allow from this computer**
4. Tap **OK**

### Verify Connection

Open PowerShell or Command Prompt:

```powershell
# Check if ADB can see your device
adb devices

# Expected output:
# List of devices attached
# ABC123456789    device
```

If you see "unauthorized" instead of "device", check the USB debugging prompt on your phone.

---

## Method 1: Using Visual Studio 2022 (Recommended)

### Setup

1. Open the solution in Visual Studio 2022
2. Right-click on the **Client** project
3. Select **Set as Startup Project**

### Build and Deploy

1. In the toolbar, look for the **Target Framework** dropdown
2. Select **net10.0-android**
3. In the **Device** dropdown, you should see your connected Android phone
   - If not visible, click **Android Device Manager** to refresh
4. Select **Android (Physical Device)** from the launch profile dropdown
5. Press **F5** or click **Start Debugging** (green play button)

Visual Studio will:
- Build the Android APK
- Install it to your phone
- Launch the app
- Attach the debugger

### Debugging

- Set breakpoints in your code by clicking the left margin
- When code hits a breakpoint, execution pauses
- Use Debug windows to inspect variables, call stack, etc.
- Step through code with F10 (Step Over) and F11 (Step Into)

---

## Method 2: Using Visual Studio Code

### Setup

1. Install the C# extension for VS Code
2. Open the project folder in VS Code

### Deploy from Terminal

Open terminal in VS Code:

```powershell
# Navigate to Client directory
cd Client

# Build and deploy to connected device
dotnet build -f net10.0-android -c Debug /p:BuildForAndroid=true
dotnet publish -f net10.0-android -c Debug /p:BuildForAndroid=true

# Install to device
adb install -r bin\Debug\net10.0-android\publish\com.hbarbitragerobot.client-Signed.apk

# Launch the app
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
```

### View Logs

```powershell
# View real-time logs from the app
adb logcat | Select-String "HBArbitrageRobot"

# Or save to file
adb logcat -d > app-logs.txt
```

---

## Method 3: Using Command Line / PowerShell

### Quick Deploy Script

Create a file `deploy-android.ps1`:

```powershell
# Deploy Android APK to Connected Phone
# Usage: .\deploy-android.ps1

Write-Host "Checking for connected Android devices..." -ForegroundColor Cyan
$devices = adb devices | Select-String "device$"
if ($devices.Count -eq 0) {
    Write-Host "ERROR: No Android devices connected!" -ForegroundColor Red
    Write-Host "Please connect your phone via USB and enable USB debugging." -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Found Android device" -ForegroundColor Green
Write-Host ""

Write-Host "Building Android APK..." -ForegroundColor Cyan
cd Client
dotnet build -f net10.0-android -c Debug /p:BuildForAndroid=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "Publishing APK..." -ForegroundColor Cyan
dotnet publish -f net10.0-android -c Debug /p:BuildForAndroid=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Publish successful" -ForegroundColor Green
Write-Host ""

# Find the APK file
$apkPath = Get-ChildItem -Path "bin\Debug\net10.0-android\publish" -Filter "*.apk" | Select-Object -First 1

if ($null -eq $apkPath) {
    Write-Host "ERROR: APK file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Installing APK to device..." -ForegroundColor Cyan
adb install -r $apkPath.FullName

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Installation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Installation successful" -ForegroundColor Green
Write-Host ""

Write-Host "Launching app..." -ForegroundColor Cyan
adb shell am start -n com.hbarbitragerobot.client/.MainActivity

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✓ App deployed and launched successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "To view logs, run:" -ForegroundColor Yellow
Write-Host "  adb logcat | Select-String 'HBArbitrageRobot'" -ForegroundColor Yellow
Write-Host ""
Write-Host "To uninstall, run:" -ForegroundColor Yellow
Write-Host "  adb uninstall com.hbarbitragerobot.client" -ForegroundColor Yellow
```

### Usage

```powershell
# Make script executable and run
.\deploy-android.ps1
```

---

## Using Launch Profiles

The project includes two Android launch profiles in `Properties/launchSettings.json`:

### 1. Android (Physical Device)
- Development mode
- Native debugging enabled
- All environment variables set

### 2. Android (Physical Device - Production)
- Production mode
- Debugging disabled
- Performance optimized

### In Visual Studio

1. Select profile from dropdown: **Android (Physical Device)**
2. Press F5 to start

### In Command Line

```powershell
# Use specific launch profile
dotnet run --project Client --launch-profile "Android (Physical Device)" -f net10.0-android /p:BuildForAndroid=true
```

---

## Troubleshooting

### Device Not Detected

**Problem:** `adb devices` shows empty or "unauthorized"

**Solutions:**
1. Unplug and replug USB cable
2. Try a different USB port (use USB 2.0 ports for better compatibility)
3. Use a different USB cable (some cables are charge-only)
4. On phone: Disable and re-enable USB debugging
5. On phone: Revoke USB debugging authorizations and reconnect
6. Restart ADB server:
   ```powershell
   adb kill-server
   adb start-server
   adb devices
   ```

### Build Errors

**Problem:** "Android workload not installed"

**Solution:**
```powershell
dotnet workload install android
```

**Problem:** "Unable to find package Avalonia.Android"

**Solution:**
```powershell
dotnet restore /p:BuildForAndroid=true
```

### Installation Fails

**Problem:** "INSTALL_FAILED_UPDATE_INCOMPATIBLE"

**Solution:**
```powershell
# Uninstall existing app first
adb uninstall com.hbarbitragerobot.client

# Then reinstall
adb install path\to\your.apk
```

**Problem:** "INSTALL_FAILED_INSUFFICIENT_STORAGE"

**Solution:** Free up space on your Android phone

### App Crashes

The app now includes **built-in crash logging** that automatically captures errors!

**Method 1: View logs within the app (Easiest)**

1. Open the HBArbitrageRobot app
2. Navigate to the **Settings** tab
3. Click **View Logs** to see recent application logs
4. Click **Share Logs** to export logs to the app's external files directory
5. The exported file location will be shown in the app (typically in Android/data/com.hbarbitragerobot.client/files/)

**Log File Locations:**
- **On Android (internal logs)**: `/data/data/com.hbarbitragerobot.client/files/logs/app_YYYYMMDD.log`
- **On Android (exported logs)**: `/sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt`
- **On Windows**: `%USERPROFILE%\Documents\HBArbitrageRobot\logs\app_YYYYMMDD.log`
- **On Desktop (exported)**: `Desktop\HBArbitrageRobot_logs_YYYYMMDD_HHMMSS.txt`

**Method 2: View crash logs via ADB:**
```powershell
# See all error logs
adb logcat *:E

# See specific app logs
adb logcat | Select-String "HBArbitrageRobot"

# Clear logs and start fresh
adb logcat -c
adb logcat
```

**Method 3: Pull log files from device:**
```powershell
# Pull exported log file (accessible without root)
adb pull /sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt ./app-logs.txt

# Or pull from app's private directory (requires debuggable app)
adb shell run-as com.hbarbitragerobot.client cat /data/data/com.hbarbitragerobot.client/files/logs/app_*.log > app-logs.txt
```

**What gets logged:**
- Application startup and initialization
- User actions and workflows
- API calls and responses
- Errors and exceptions with stack traces
- Unhandled exceptions and crashes

**Log Rotation:**
- Logs are automatically rotated when they reach 5MB
- Up to 5 log files are kept (approximately 25MB total)
- Oldest logs are automatically deleted


### Can't Connect to Server API

If your app can't reach `https://localhost:64743`:

**Problem:** Localhost on Android doesn't point to Windows PC

**Solution:** Use your PC's IP address instead

1. Find your Windows PC IP:
   ```powershell
   ipconfig
   # Look for IPv4 Address (e.g., 192.168.1.100)
   ```

2. Update API URL in launchSettings.json:
   ```json
   "API_URL": "https://192.168.1.100:64743"
   ```

3. Or use Android emulator special address:
   - For Android Emulator: `10.0.2.2` points to host machine
   - For physical device: Use actual IP address

### Permissions Issues

If app crashes due to missing permissions:

1. Check app permissions on Android:
   - Settings → Apps → HBArbitrageRobot → Permissions
2. Grant all required permissions manually
3. Add permission requests to AndroidManifest.xml if needed

---

## Advanced: Wireless Debugging (Android 11+)

### Setup

On your Android phone:
1. Settings → Developer Options → Wireless Debugging
2. Enable **Wireless Debugging**
3. Tap **Pair device with pairing code**
4. Note the IP address and pairing code

On Windows:
```powershell
# Pair device (do this once)
adb pair <IP:PORT>
# Enter the pairing code when prompted

# Connect to device
adb connect <IP:PORT>

# Verify
adb devices
```

Now you can deploy without USB cable!

---

## Hot Reload and Live Updates

### Using .NET Hot Reload

When debugging with Visual Studio:

1. Make code changes while app is running
2. Save the file (Ctrl+S)
3. Hot Reload should apply changes automatically
4. If not, click **Hot Reload** button in toolbar

### Manual Redeploy

For major changes that need full redeploy:

```powershell
# Quick redeploy
adb install -r bin\Debug\net10.0-android\publish\*.apk
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
```

---

## Performance Tips

### Faster Builds

1. **Use Debug configuration for testing** (not Release)
2. **Disable AOT compilation** for debug builds (already configured)
3. **Use incremental builds**
   ```powershell
   dotnet build -f net10.0-android /p:BuildForAndroid=true
   # Skip clean builds during development
   ```

### Faster Deployment

1. **Keep app installed** - use `adb install -r` to reinstall
2. **Use wireless debugging** to avoid cable issues
3. **Use Release builds** only for final testing

---

## Useful ADB Commands

```powershell
# List all connected devices
adb devices -l

# Install APK (replace if exists)
adb install -r app.apk

# Uninstall app
adb uninstall com.hbarbitragerobot.client

# Launch app
adb shell am start -n com.hbarbitragerobot.client/.MainActivity

# Stop app
adb shell am force-stop com.hbarbitragerobot.client

# Clear app data
adb shell pm clear com.hbarbitragerobot.client

# View real-time logs
adb logcat

# View error logs only
adb logcat *:E

# Save logs to file
adb logcat -d > logs.txt

# Take screenshot
adb shell screencap /sdcard/screen.png
adb pull /sdcard/screen.png

# Record screen
adb shell screenrecord /sdcard/demo.mp4

# Get device info
adb shell getprop ro.build.version.release  # Android version
adb shell getprop ro.product.model          # Device model

# File transfer
adb push local-file.txt /sdcard/
adb pull /sdcard/remote-file.txt

# Shell access
adb shell
```

---

## IDE Configuration

### Visual Studio 2022

**Recommended Settings:**
1. Tools → Options → Debugging → General
   - ✓ Enable Just My Code
   - ✓ Enable .NET Hot Reload
2. Tools → Options → Android
   - Set SDK Path if not auto-detected
   - Enable fast deployment

### Visual Studio Code

**Recommended Extensions:**
1. C# (Microsoft)
2. C# Dev Kit
3. Android for VS Code

**tasks.json** example:
```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Deploy to Android",
      "type": "shell",
      "command": "powershell",
      "args": [
        "-File",
        "deploy-android.ps1"
      ],
      "group": {
        "kind": "build",
        "isDefault": true
      },
      "problemMatcher": []
    }
  ]
}
```

---

## Summary

**Quick Start:**
1. Enable USB debugging on Android phone
2. Connect via USB to Windows
3. Run: `adb devices` to verify
4. Open project in Visual Studio
5. Select **Android (Physical Device)** profile
6. Press F5

**For Command Line:**
```powershell
cd Client
dotnet build -f net10.0-android -c Debug /p:BuildForAndroid=true
dotnet publish -f net10.0-android -c Debug /p:BuildForAndroid=true
adb install -r bin\Debug\net10.0-android\publish\*.apk
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
adb logcat | Select-String "HBArbitrageRobot"
```

**Need Help?**
- Check ADB connection: `adb devices`
- View logs: `adb logcat`
- Reinstall: `adb uninstall com.hbarbitragerobot.client`
- See [ANDROID_TESTING_WINDOWS.md](ANDROID_TESTING_WINDOWS.md) for emulator options

---

## Next Steps

After successful deployment:
1. Test all app features on real device
2. Check network connectivity (use PC IP instead of localhost)
3. Test different screen sizes and orientations
4. Verify permissions work correctly
5. Test app performance and battery usage

For production deployment, see [BUILD_WORKFLOW.md](BUILD_WORKFLOW.md) for automated builds.
