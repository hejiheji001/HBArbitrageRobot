# Android Client Deployment Guide

This guide explains how to deploy the HBArbitrageRobot Client to an Android device for mobile arbitrage trading.

## Table of Contents
- [Overview](#overview)
- [Deployment Options](#deployment-options)
- [Option 1: APK Deployment (Recommended)](#option-1-apk-deployment-recommended)
- [Option 2: Remote Execution](#option-2-remote-execution)
- [Option 3: Termux Terminal](#option-3-termux-terminal)
- [Comparison](#comparison)
- [Best Practices](#best-practices)

## Overview

The HBArbitrageRobot Client is a .NET application that uses Playwright for browser automation. There are several ways to run it on Android:

1. **APK with .NET Android** (Recommended) - Native Android app
2. **Remote Execution** - Client runs on AWS, Android just monitors
3. **Termux** - Run .NET in Android terminal emulator

## Deployment Options

### Architecture Decision

```
┌─────────────────────────────────────────────────────────────┐
│                      Trading System                          │
│                                                              │
│  ┌──────────────┐          ┌──────────────┐                │
│  │   AWS Lambda │          │   Android    │                │
│  │   (Server)   │◄────────►│   Device     │                │
│  │              │   API    │              │                │
│  │ - Fetch LOF  │          │ - Monitor    │                │
│  │ - Filter     │          │ - Control    │                │
│  │ - Cache      │          │ - Alerts     │                │
│  └──────────────┘          └──────────────┘                │
│         ▲                          │                        │
│         │                          │                        │
│         │                          ▼                        │
│         │                  ┌──────────────┐                │
│         │                  │   Client     │                │
│         │                  │  (Choose:)   │                │
│         │                  │              │                │
│         │                  │ 1. Native    │                │
│         │                  │    APK       │                │
│         │                  │ 2. AWS       │                │
│         └──────────────────│    Lambda    │                │
│                            │ 3. Termux    │                │
│                            └──────────────┘                │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Option 1: APK Deployment (Recommended)

Create a native Android app using .NET MAUI or Xamarin that runs the Client logic directly on the Android device.

### Advantages
✅ Native Android experience
✅ Works offline (except for API calls)
✅ Full control over execution
✅ No additional infrastructure costs
✅ Access to device features (notifications, background execution)

### Disadvantages
❌ Requires app development work
❌ Need to adapt Playwright (might need WebView automation instead)
❌ App store approval (if distributing publicly)
❌ Manual APK installation (if not using app store)

### Implementation Steps

#### Step 1: Create .NET MAUI Project

```bash
# Install .NET MAUI workload
dotnet workload install maui

# Create new MAUI project
dotnet new maui -n HBArbitrageRobotApp
cd HBArbitrageRobotApp
```

#### Step 2: Adapt Client Code

Since Playwright doesn't work on Android, use Android WebView for automation:

**File: `MainPage.xaml.cs`**
```csharp
using Microsoft.Maui.Controls;
using System.Net.Http;
using System.Text.Json;

namespace HBArbitrageRobotApp;

public partial class MainPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private readonly string _apiUrl = "https://your-lambda-url.lambda-url.us-east-1.on.aws";
    private WebView? _webView;

    public MainPage()
    {
        InitializeComponent();
        InitializeWebView();
    }

    private void InitializeWebView()
    {
        _webView = new WebView
        {
            Source = "https://m.touker.com"
        };
        
        // Add WebView to page
        Content = new StackLayout
        {
            Children = { 
                new Label { Text = "HB Arbitrage Robot" },
                new Button { Text = "Start Trading", Command = new Command(OnStartTrading) },
                _webView 
            }
        };
    }

    private async void OnStartTrading()
    {
        // Fetch LOF data from AWS Lambda Server
        var response = await _httpClient.GetAsync($"{_apiUrl}/api/data");
        var lofData = await response.Content.ReadAsStringAsync();
        
        // Parse and process LOF data
        var funds = JsonSerializer.Deserialize<List<LofFund>>(lofData);
        
        // Automate trading via WebView
        await AutomateTrading(funds);
    }

    private async Task AutomateTrading(List<LofFund> funds)
    {
        // Navigate and interact with touker.com via WebView
        // This is simpler than Playwright but less powerful
        
        // 1. Login
        await InjectJavaScript(@"
            document.querySelector('#username').value = 'your_username';
            document.querySelector('#password').value = 'your_password';
            document.querySelector('#login-button').click();
        ");
        
        await Task.Delay(2000); // Wait for login
        
        // 2. Create orders for each fund
        foreach (var fund in funds)
        {
            await InjectJavaScript($@"
                // Navigate to order creation
                // Fill in fund details
                // Submit order
            ");
        }
    }

    private async Task InjectJavaScript(string script)
    {
        if (_webView != null)
        {
            await _webView.EvaluateJavaScriptAsync(script);
        }
    }
}

public class LofFund
{
    public string LofFund { get; set; } = "";
    public string ApplyStatus { get; set; } = "";
}
```

#### Step 3: Configure Android Manifest

**File: `Platforms/Android/AndroidManifest.xml`**
```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application android:allowBackup="true" android:icon="@mipmap/appicon" android:roundIcon="@mipmap/appicon_round" android:supportsRtl="true">
    </application>
    
    <!-- Permissions -->
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
    <uses-permission android:name="android.permission.WAKE_LOCK" />
    <uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM" />
    
    <!-- Minimum SDK version -->
    <uses-sdk android:minSdkVersion="21" android:targetSdkVersion="34" />
</manifest>
```

#### Step 4: Add Background Scheduling

For scheduled execution at trading hours, use Android WorkManager:

**File: `TradingWorker.cs`**
```csharp
using AndroidX.Work;
using Android.Content;

public class TradingWorker : Worker
{
    public TradingWorker(Context context, WorkerParameters workerParams) 
        : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        try
        {
            // Execute trading logic
            // This runs in background even when app is closed
            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            return Result.InvokeFailure();
        }
    }
}

// Schedule work
public void ScheduleDailyTrading()
{
    var constraints = new Constraints.Builder()
        .SetRequiredNetworkType(NetworkType.Connected)
        .Build();

    var workRequest = PeriodicWorkRequest.Builder.From<TradingWorker>(TimeSpan.FromDays(1))
        .SetInitialDelay(TimeSpan.FromHours(14)) // 14:00 (adjust for timezone)
        .SetConstraints(constraints)
        .Build();

    WorkManager.Instance.Enqueue(workRequest);
}
```

#### Step 5: Build APK

```bash
# Build for Android
dotnet build -t:Run -f net10.0-android

# Create release APK
dotnet publish -f net10.0-android -c Release

# APK location
# bin/Release/net10.0-android/publish/com.yourcompany.hbarbitragerobot-Signed.apk
```

#### Step 6: Install on Android

**Option A: USB Cable**
```bash
# Enable USB debugging on Android device
# Settings → About Phone → Tap "Build Number" 7 times → Developer Options → USB Debugging

# Install APK
adb install bin/Release/net10.0-android/publish/com.yourcompany.hbarbitragerobot-Signed.apk
```

**Option B: Direct Download**
1. Upload APK to cloud storage (Google Drive, Dropbox)
2. Download on Android device
3. Allow installation from unknown sources
4. Install APK

**Option C: Google Play Store** (for public distribution)
1. Create Google Play Developer account ($25 one-time fee)
2. Upload APK
3. Submit for review
4. Publish after approval

### Simplified APK Approach

Since building a full MAUI app is complex, consider this simplified approach:

**Create a monitoring/control app that:**
- Displays Server API data (LOF funds)
- Shows execution status from AWS Lambda Client
- Sends notifications when trading completes
- Provides manual trigger button
- Doesn't do browser automation (let AWS Lambda Client handle that)

This way:
- ✅ Simpler to build (just HTTP API calls)
- ✅ No Playwright/WebView complexity
- ✅ AWS Lambda handles heavy lifting
- ✅ Android app is just a UI/monitor

## Option 2: Remote Execution

Run the Client on AWS Lambda (as described in DEPLOYMENT_AWS.md) and use the Android device only for monitoring and control.

### Advantages
✅ No app development needed
✅ Client runs in cloud (reliable, scheduled)
✅ Android only needs browser or simple app
✅ Easy to update (just redeploy Lambda)
✅ No battery drain on phone

### Disadvantages
❌ Requires AWS Lambda deployment
❌ Monthly AWS costs (~$0.80)
❌ Internet dependency
❌ Less direct control

### Implementation Steps

#### Step 1: Deploy to AWS Lambda

Follow [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md) to deploy Server and Client to AWS Lambda.

#### Step 2: Create Simple Monitoring App

**Option A: Web App (No Installation)**

Create a simple HTML page hosted on GitHub Pages:

```html
<!DOCTYPE html>
<html>
<head>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>HB Arbitrage Monitor</title>
    <style>
        body { font-family: Arial; padding: 20px; }
        .fund { border: 1px solid #ddd; padding: 10px; margin: 10px 0; }
        button { padding: 10px 20px; font-size: 16px; }
    </style>
</head>
<body>
    <h1>HB Arbitrage Monitor</h1>
    <button onclick="fetchData()">Refresh Data</button>
    <button onclick="triggerClient()">Trigger Trading</button>
    <div id="status"></div>
    <div id="funds"></div>
    
    <script>
        const API_URL = 'https://your-lambda-url.lambda-url.us-east-1.on.aws';
        
        async function fetchData() {
            const response = await fetch(`${API_URL}/api/data`);
            const data = await response.json();
            
            document.getElementById('funds').innerHTML = data.map(fund => `
                <div class="fund">
                    <strong>${fund.lofFund}</strong> - ${fund.applyStatus}
                </div>
            `).join('');
        }
        
        async function triggerClient() {
            // Manually invoke Lambda
            document.getElementById('status').textContent = 'Trading triggered...';
            // You'd need API Gateway + Lambda integration for this
        }
        
        // Auto-refresh every minute
        setInterval(fetchData, 60000);
        fetchData();
    </script>
</body>
</html>
```

**Option B: Native App (Simplified)**

Minimal .NET MAUI app that just displays data:

```csharp
public class MainPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private readonly string _apiUrl = "https://your-lambda-url.lambda-url.us-east-1.on.aws";
    private ListView _listView;
    
    public MainPage()
    {
        InitializeComponent();
        
        Content = new StackLayout
        {
            Children = {
                new Label { Text = "HB Arbitrage Monitor", FontSize = 24 },
                new Button { Text = "Refresh", Command = new Command(async () => await RefreshData()) },
                _listView = new ListView()
            }
        };
        
        _ = RefreshData();
    }
    
    private async Task RefreshData()
    {
        var response = await _httpClient.GetAsync($"{_apiUrl}/api/data");
        var data = await response.Content.ReadAsStringAsync();
        var funds = JsonSerializer.Deserialize<List<LofFund>>(data);
        
        _listView.ItemsSource = funds;
    }
}
```

#### Step 3: Monitor on Android

1. Open web app or native app on Android
2. View current LOF funds
3. Check execution status
4. Receive notifications (if implemented)

### Notifications

Add push notifications using:
- **Firebase Cloud Messaging (FCM)**: Free, requires Google account
- **AWS SNS**: Integrates with Lambda, sends SMS/email

Example Lambda notification:
```python
import boto3

sns = boto3.client('sns')

def send_notification(message):
    sns.publish(
        PhoneNumber='+86xxxxxxxxxxx',  # Your phone number
        Message=message
    )

# In your Lambda
send_notification(f'Trading completed: {len(funds)} funds processed')
```

## Option 3: Termux Terminal

Run the .NET Client directly on Android using Termux (a Linux terminal emulator).

### Advantages
✅ No app development
✅ Full .NET runtime on Android
✅ Can run existing Client code
✅ Good for testing/development

### Disadvantages
❌ Requires terminal knowledge
❌ Not user-friendly
❌ Playwright may not work well on Android
❌ Battery intensive
❌ No background execution (phone must stay on)

### Implementation Steps

#### Step 1: Install Termux

1. Download Termux from F-Droid (recommended) or Google Play
   - F-Droid: https://f-droid.org/packages/com.termux/
   - Avoid Google Play version (outdated)

#### Step 2: Install .NET SDK

```bash
# Update packages
pkg update && pkg upgrade

# Install dependencies
pkg install wget proot-distro

# Install Ubuntu in Termux
proot-distro install ubuntu
proot-distro login ubuntu

# Install .NET 10 (inside Ubuntu)
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
export PATH="$HOME/.dotnet:$PATH"

# Verify
dotnet --version
```

#### Step 3: Clone and Build

```bash
# Install git
apt update && apt install git

# Clone repository
git clone https://github.com/hejiheji001/HBArbitrageRobot.git
cd HBArbitrageRobot/Client

# Build
dotnet build
```

#### Step 4: Run Client

```bash
# Set environment variables
export USERNAME="your_username"
export PASSWORD="your_password"
export API_URL="https://your-lambda-url.lambda-url.us-east-1.on.aws"
export HEADLESS="true"

# Run
dotnet run
```

**Note**: Playwright may not work on Android ARM architecture. You might need to modify the Client to use a different automation method.

#### Step 5: Schedule Execution

Use Termux cron (Termux:Boot app required):

```bash
# Install cronie
pkg install cronie

# Create cron job
crontab -e

# Add line (14:20 daily)
20 14 * * * cd /data/data/com.termux/files/home/HBArbitrageRobot/Client && dotnet run
```

## Comparison

| Feature | APK (Native) | Remote (AWS Lambda) | Termux |
|---------|-------------|---------------------|--------|
| Ease of Setup | ⭐⭐ Complex | ⭐⭐⭐⭐⭐ Easy | ⭐⭐⭐ Medium |
| User Experience | ⭐⭐⭐⭐⭐ Native | ⭐⭐⭐⭐ Web/App | ⭐⭐ Terminal |
| Offline Support | ⭐⭐⭐⭐⭐ Full | ⭐ None | ⭐⭐⭐⭐ Good |
| Battery Impact | ⭐⭐⭐ Medium | ⭐⭐⭐⭐⭐ Minimal | ⭐⭐ High |
| Reliability | ⭐⭐⭐⭐ Good | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐⭐ Fair |
| Cost | ⭐⭐⭐⭐⭐ Free | ⭐⭐⭐⭐ ~$0.80/month | ⭐⭐⭐⭐⭐ Free |
| Maintenance | ⭐⭐⭐ Medium | ⭐⭐⭐⭐⭐ Easy | ⭐⭐⭐ Medium |

## Best Practices

### Recommended Approach

**For most users**: **Option 2 (Remote Execution)**

1. Deploy Server and Client to AWS Lambda (see DEPLOYMENT_AWS.md)
2. Use Android browser to access Server API for monitoring
3. Optionally create simple monitoring app (web or native)
4. Let Lambda handle all automation

**Why?**
- ✅ Easiest to set up and maintain
- ✅ Most reliable (AWS infrastructure)
- ✅ Minimal phone battery usage
- ✅ Cloud scheduling (always runs on time)
- ✅ Easy to update (just redeploy)
- ✅ Low cost (~$0.80/month)

### Security Considerations

1. **Store credentials securely**
   - Use AWS Secrets Manager for Lambda
   - Use Android Keystore for APK
   - Never hardcode credentials

2. **Enable HTTPS**
   - Lambda Function URLs use HTTPS by default
   - Ensure all API calls use HTTPS

3. **Authentication**
   - Enable IAM auth for Lambda Function URL if needed
   - Use OAuth/JWT for custom Android app

4. **Network Security**
   - Use VPN on public WiFi
   - Verify SSL certificates

### Monitoring and Alerts

1. **CloudWatch Alarms** (for Lambda)
   - Alert on errors
   - Alert on execution timeouts
   - Alert on API failures

2. **Push Notifications** (for Android)
   - Trading completion
   - Errors/failures
   - Daily summary

3. **Logging**
   - Lambda logs in CloudWatch
   - Android app logs locally
   - Centralized logging (optional)

### Backup and Recovery

1. **Backup Configuration**
   - Save Terraform state to S3
   - Export Lambda environment variables
   - Document credentials securely

2. **Disaster Recovery**
   - Keep infrastructure as code (Terraform)
   - Automated deployment scripts
   - Regular testing

### Testing

1. **Test in Development**
   - Use test credentials
   - Small investment amounts
   - Verify all workflows

2. **Staging Environment**
   - Separate Lambda functions for testing
   - Test schedule triggers
   - Monitor for issues

3. **Production Monitoring**
   - Daily checks
   - Review logs
   - Verify trades executed

## Next Steps

1. ✅ Choose deployment option (recommend Option 2)
2. ✅ Follow DEPLOYMENT_AWS.md to deploy Server and Client
3. ✅ Create simple monitoring interface
4. ✅ Test end-to-end workflow
5. ✅ Set up monitoring and alerts
6. ✅ Monitor first few trading days

## Additional Resources

- [.NET MAUI Documentation](https://docs.microsoft.com/dotnet/maui/)
- [Android Developer Guide](https://developer.android.com/guide)
- [Termux Wiki](https://wiki.termux.com/)
- [AWS Lambda Best Practices](https://docs.aws.amazon.com/lambda/latest/dg/best-practices.html)
- [Firebase Cloud Messaging](https://firebase.google.com/docs/cloud-messaging)
