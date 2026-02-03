# Cross-Platform GUI Client Implementation Guide

This document describes the implementation of the cross-platform GUI client with multi-user support for HBArbitrageRobot.

## Overview

The Client has been transformed from a console application to a modern cross-platform GUI application using Avalonia UI framework. The application supports:

- ✅ Multiple user credentials with secure encrypted storage
- ✅ Sequential execution of arbitrage for multiple users
- ✅ Headless Playwright automation
- ✅ Detailed per-user execution logging
- ✅ Cross-platform support (Windows, macOS, Linux, Android)

## Architecture

### Technology Stack

- **UI Framework**: Avalonia UI 11.1.3 (Cross-platform XAML-based)
- **Architecture**: MVVM (Model-View-ViewModel) with ReactiveUI
- **Database**: SQLite with SQLite-net-pcl
- **Encryption**: System.Security.Cryptography.ProtectedData (Windows DPAPI + fallback)
- **Browser Automation**: Microsoft.Playwright 1.57.0

### Project Structure

```
Client/
├── Models/
│   ├── UserCredential.cs          # User account model
│   ├── ExecutionResult.cs         # Execution log model
│   └── [existing models]
├── Services/
│   ├── CredentialStorageService.cs    # Secure credential storage
│   ├── MultiUserExecutionService.cs   # Multi-user execution engine
│   └── [existing services]
├── ViewModels/
│   ├── MainWindowViewModel.cs         # Main navigation
│   ├── UserManagementViewModel.cs     # User CRUD operations
│   ├── ExecutionViewModel.cs          # Arbitrage execution
│   ├── ResultsViewModel.cs            # View execution logs
│   └── SettingsViewModel.cs           # Application settings
├── Views/
│   ├── MainWindow.axaml               # Main window with nav
│   ├── UserManagementView.axaml       # User management UI
│   ├── ExecutionView.axaml            # Execution UI
│   ├── ResultsView.axaml              # Results viewer
│   └── SettingsView.axaml             # Settings UI
├── App.axaml                          # Application definition
├── Program.cs                         # Entry point
└── Client.csproj                      # Project file
```

## Features Implemented

### 1. Secure Credential Storage

**CredentialStorageService** provides:
- SQLite database storage in user's LocalApplicationData folder
- Encrypted password storage using:
  - Windows: DPAPI (Data Protection API)
  - Other platforms: XOR encryption with machine-specific entropy
- Automatic database initialization
- CRUD operations for user credentials

**UserCredential Model:**
```csharp
- Id: Primary key
- Username: Login username
- EncryptedPassword: Encrypted login password
- EncryptedTransactionPassword: Encrypted transaction password
- DisplayName: User-friendly name
- IsEnabled: Enable/disable user
- CreatedAt: Creation timestamp
- LastExecutedAt: Last execution time
```

### 2. Multi-User Execution

**MultiUserExecutionService** provides:
- Sequential execution for all enabled users
- Progress events for real-time UI updates
- Per-user execution results
- Error handling and recovery
- Headless Playwright automation

**ExecutionResult Model:**
```csharp
- Username: User identifier
- ExecutionTime: When executed
- Success: Execution status
- ErrorMessage: Error details if failed
- LofItemsProcessed: Number of LOF items
- StockHoldersCount: Number of stock holders
- TotalInvestment: Total investment amount
- LogMessages: Detailed execution log
- DurationSeconds: Execution duration
```

### 3. User Interface

**MainWindow:**
- Navigation sidebar with 4 sections:
  - 👥 Users: Manage user credentials
  - ▶️ Execute: Run arbitrage for all users
  - 📊 Results: View execution history
  - ⚙️ Settings: Application settings

**UserManagementView:**
- DataGrid listing all users
- Add/Edit/Delete operations
- In-place editing form
- Enable/disable users
- Display last execution time

**ExecutionView:**
- Configuration inputs:
  - API URL (Server endpoint)
  - Purchase Time (HH:MM format)
  - Max Investment (yuan per holder)
- Start execution button
- Real-time log output
- Progress indicator
- Status message

**ResultsView:**
- DataGrid with execution history
- Columns: Status, Username, Time, Items, Investment, Duration, Error
- Details panel showing full execution log
- Auto-refresh when navigating to view

## Implementation Details

### Credential Encryption

**Windows (DPAPI):**
```csharp
var encryptedBytes = ProtectedData.Protect(
    plainBytes, 
    entropy, 
    DataProtectionScope.CurrentUser
);
```

**Other Platforms (XOR):**
```csharp
var encrypted = new byte[plainBytes.Length];
for (int i = 0; i < plainBytes.Length; i++)
{
    encrypted[i] = (byte)(plainBytes[i] ^ _entropy[i % _entropy.Length]);
}
```

### Multi-User Execution Flow

1. Load all enabled users from database
2. For each user sequentially:
   a. Fetch LOF data from Server API
   b. Initialize Playwright browser (headless)
   c. Login to touker.com
   d. Fetch stock holder list
   e. Calculate investments
   f. Create arbitrage orders
   g. Log results to database
   h. Fire completion event
3. Repeat for next user

### Database Schema

**UserCredentials Table:**
```sql
CREATE TABLE UserCredentials (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT(100),
    EncryptedPassword TEXT,
    DisplayName TEXT(200),
    IsEnabled INTEGER,
    CreatedAt TEXT,
    LastExecutedAt TEXT,
    EncryptedTransactionPassword TEXT
);
```

**ExecutionResults Table:**
```sql
CREATE TABLE ExecutionResults (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserCredentialId INTEGER,
    Username TEXT,
    ExecutionTime TEXT,
    Success INTEGER,
    ErrorMessage TEXT,
    LofItemsProcessed INTEGER,
    StockHoldersCount INTEGER,
    TotalInvestment REAL,
    LogMessages TEXT,
    DurationSeconds REAL
);
```

## Building and Running

### Prerequisites

```bash
# .NET 10 SDK
dotnet --version  # Should be 10.0.x

# Restore packages
cd Client
dotnet restore

# Build
dotnet build
```

### Running Modes

The Client supports two distinct running modes:

#### GUI Mode (Default)

Launch the graphical user interface:

```bash
cd Client
dotnet run
```

Features:
- Multi-user credential management
- Manual execution control
- View execution history and logs
- Configure application settings
- Playwright runs in headless mode (no browser window overlap)

#### CLI Mode (Console)

Run in console mode for automated/scheduled deployments:

```bash
cd Client
# With arguments
dotnet run -- testuser testpass http://localhost:5000 testpwd 14:15 1000

# With environment variables
export USERNAME=testuser
export PASSWORD=testpass
export API_URL=http://localhost:5000
dotnet run -- --cli
```

Features:
- Continuous arbitrage workflow
- Automatic scheduling (wakes at 14:20 Shanghai time)
- Headless Playwright automation
- Suitable for Docker/server deployments

### Mode Detection

- **CLI Mode** triggered when:
  - First argument is `--cli` or `-c`
  - First argument doesn't start with `--` (treated as username)
  
- **GUI Mode** triggered when:
  - No arguments provided
  - First argument starts with `--` (except `--cli`)

### Browser Headless Behavior

- **CLI Mode**: Always headless
- **GUI Mode**: Always headless (prevents browser window from interfering with GUI)
- **Development/Testing**: Set `HEADLESS=false` or `DOTNET_ENVIRONMENT=Development` to see browser UI

### Running on Windows

**GUI Mode:**
```bash
cd Client
dotnet run
```

**CLI Mode:**
```bash
cd Client
dotnet run -- --cli  # or with arguments
```

The application will:
1. Create database at: `%LOCALAPPDATA%\HBArbitrageRobot\credentials.db` (GUI mode)
2. Launch GUI window (GUI mode) or run console workflow (CLI mode)
3. Start on Users tab (GUI mode)

### Running on macOS

**GUI Mode:**
```bash
cd Client
dotnet run
```

**CLI Mode:**
```bash
cd Client
dotnet run -- --cli
```

Database location: `~/.local/share/HBArbitrageRobot/credentials.db`

### Running on Linux

**GUI Mode:**
```bash
cd Client
dotnet run
```

**CLI Mode:**
```bash
cd Client
dotnet run -- --cli
```

Database location: `~/.local/share/HBArbitrageRobot/credentials.db`

## Android Deployment

### Option 1: Avalonia Android (Recommended)

Avalonia supports Android through a separate project configuration:

1. **Create Android Project:**
```bash
# Add Android workload
dotnet workload install android

# Create Android project
dotnet new avalonia.android -n Client.Android
```

2. **Configure Android Project:**
```xml
<!-- Client.Android/Client.Android.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <SupportedOSPlatformVersion>21</SupportedOSPlatformVersion>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Client/Client.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Android" Version="11.1.3" />
  </ItemGroup>
</Project>
```

3. **Build APK:**
```bash
cd Client.Android
dotnet build -c Release
```

APK location: `bin/Release/net10.0-android/com.hbarbitrage.client-Signed.apk`

### Option 2: Playwright Android Support

Playwright supports Android automation through the Android device connection:

```csharp
// Connect to Android device
var playwright = await Playwright.CreateAsync();
var device = await playwright.Android.ConnectAsync("device-id");
var context = await device.NewContextAsync();
```

**Note**: Playwright Android support requires:
- ADB (Android Debug Bridge)
- Android device in developer mode
- USB debugging enabled

## Known Issues and Limitations

### Build Errors

~~Current build has XAML errors related to DataGrid converters.~~ **FIXED**: ObjectConverters class has been created with the required converters:
- `IsTrue`: Converts boolean to string (✅|❌)
- `IsNull`: Converts object null check to string (Add User|Edit User)
- `IsNotNull`: Converts object null check to boolean (for IsVisible binding)

The converters are now properly referenced in XAML files with the `converters:` namespace prefix.

### Platform-Specific Issues

**Windows:**
- DPAPI encryption works perfectly
- No known issues

**macOS:**
- XOR encryption used (less secure than DPAPI)
- Consider using macOS Keychain for production

**Linux:**
- XOR encryption used
- Consider using libsecret or GNOME Keyring

**Android:**
- Playwright may not work natively on Android
- Consider using Android WebView as alternative
- Or run Client on AWS Lambda (see DEPLOYMENT_AWS.md)

## Testing

### Unit Testing

Create test project:
```bash
dotnet new xunit -n Client.Tests
cd Client.Tests
dotnet add reference ../Client/Client.csproj
```

Test examples:
```csharp
[Fact]
public void CredentialStorage_EncryptDecrypt_ReturnsOriginal()
{
    var service = new CredentialStorageService();
    var user = new UserCredential
    {
        Username = "test",
        DecryptedPassword = "password123"
    };
    
    service.SaveUser(user);
    var retrieved = service.GetUser(user.Id);
    
    Assert.Equal("password123", retrieved.DecryptedPassword);
}
```

### Manual Testing

1. **User Management:**
   - Add new user with username and password
   - Edit existing user
   - Delete user
   - Enable/disable user

2. **Execution:**
   - Configure API URL, purchase time, max investment
   - Click "Start Execution"
   - Verify log messages appear
   - Check execution completes successfully

3. **Results:**
   - Navigate to Results tab
   - Verify execution history displayed
   - Click on a result to see detailed log
   - Verify timestamps and data are correct

## Security Considerations

### Credential Storage

✅ **Good:**
- Passwords encrypted at rest
- Per-user encryption scope
- Machine-specific entropy

⚠️ **Could Improve:**
- Use platform-specific secure storage (Keychain, Credential Manager, Keyring)
- Add master password for additional security
- Implement credential export/import with encryption

### Network Security

- All API calls should use HTTPS
- Validate SSL certificates
- Consider API key authentication for Server

### Database Security

- SQLite file permissions should be restricted
- Consider encrypting entire database
- Regular backups recommended

## Future Enhancements

### High Priority

1. **Fix Build Errors**: Complete XAML fixes for DataGrid
2. **Android Configuration**: Complete Android project setup
3. **Icon and Branding**: Add application icon and assets
4. **Input Validation**: Add validation for user inputs
5. **Error Dialogs**: Show user-friendly error messages

### Medium Priority

1. **Settings Page**: Implement application settings
2. **Scheduled Execution**: Add timer for automatic execution at specified time
3. **Notifications**: Desktop notifications for execution completion
4. **Export Results**: Export execution history to CSV/Excel
5. **Dark Mode**: Add theme toggle

### Low Priority

1. **Multiple Languages**: i18n support
2. **Custom Themes**: User-customizable themes
3. **Charts**: Visualize execution statistics
4. **Cloud Sync**: Sync credentials across devices (encrypted)

## Troubleshooting

### Database Issues

**Error: "Database is locked"**
```csharp
// Solution: Ensure only one SQLiteConnection is open at a time
using var db = new SQLiteConnection(_dbPath);
// ... operations
// Connection automatically closed by using
```

**Error: "Table already exists"**
```csharp
// Solution: Use CreateTable with proper flags
db.CreateTable<UserCredential>();
// Or drop and recreate
db.DropTable<UserCredential>();
db.CreateTable<UserCredential>();
```

### Playwright Issues

**Error: "Executable doesn't exist"**
```bash
# Solution: Install Playwright browsers
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

**Error: "Failed to launch browser"**
```csharp
// Solution: Check headless parameter
var (playwright, browser) = await browserService.InitializeBrowserAsync(headless: true);
```

### UI Issues

**Window doesn't appear:**
```csharp
// Check Program.cs has correct initialization
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
```

**DataBinding not working:**
```csharp
// Ensure ViewModel implements INotifyPropertyChanged
public class MyViewModel : ReactiveObject
{
    private string _myProperty;
    public string MyProperty
    {
        get => _myProperty;
        set => this.RaiseAndSetIfChanged(ref _myProperty, value);
    }
}
```

## Comparison: GUI vs CLI vs AWS Lambda

| Feature | GUI Client | CLI Client (Original) | AWS Lambda (DEPLOYMENT_AWS.md) |
|---------|------------|----------------------|--------------------------------|
| **Multi-User** | ✅ Yes | ❌ No | ✅ Yes (with modifications) |
| **User Interface** | ✅ Modern GUI | ⚠️ Console only | ❌ No UI |
| **Cross-Platform** | ✅ Win/Mac/Linux | ✅ Win/Mac/Linux | ☁️ Cloud only |
| **Credential Storage** | ✅ Encrypted DB | ⚠️ Env vars only | ✅ Terraform vars |
| **Execution Logs** | ✅ Stored in DB | ⚠️ Console only | ✅ CloudWatch |
| **Scheduling** | 🚧 Manual/Timer | ✅ Built-in scheduler | ✅ EventBridge |
| **Cost** | 💵 Free (local) | 💵 Free (local) | 💵 ~$0.80/month |
| **Maintenance** | ⚠️ User managed | ⚠️ User managed | ✅ AWS managed |
| **Android Support** | 🚧 Possible | ❌ Difficult | ✅ Via monitoring app |

## Recommendation

For most users, we recommend the **hybrid approach**:

1. **Deploy Server to AWS Lambda** (see DEPLOYMENT_AWS.md)
   - Cost-effective (~$0.80/month)
   - Automatic scheduling (Mon-Fri 14:00)
   - Reliable AWS infrastructure

2. **Use GUI Client for monitoring and manual execution**
   - Windows/Mac desktop for full GUI
   - Store multiple user credentials
   - Manual execution when needed
   - View detailed execution logs

3. **Android for mobile monitoring**
   - Simple web interface or monitoring app
   - Check execution status
   - Receive notifications

This provides the best balance of cost, reliability, and user experience.

## Additional Resources

- [Avalonia UI Documentation](https://docs.avaloniaui.net/)
- [ReactiveUI Documentation](https://www.reactiveui.net/)
- [SQLite-net Documentation](https://github.com/praeclarum/sqlite-net)
- [Playwright .NET Documentation](https://playwright.dev/dotnet/)
- [AWS Lambda Deployment Guide](../DEPLOYMENT_AWS.md)
- [Android Deployment Guide](../DEPLOYMENT_ANDROID.md)

## Contributing

When contributing to the GUI client:

1. Follow MVVM pattern
2. Use ReactiveUI for property changes
3. Keep Views simple, logic in ViewModels
4. Add XML documentation to public APIs
5. Test on Windows and macOS
6. Ensure Android compatibility

## License

Same as main project (MIT License)
