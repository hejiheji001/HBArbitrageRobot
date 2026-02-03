# Multi-Platform Build and Release Workflow

This document describes the automated build and release workflow for the HBArbitrageRobot Client application.

## Overview

The workflow automatically builds the Client application for multiple platforms:
- **Windows** (x64)
- **macOS** (x64 and ARM64/Apple Silicon)
- **Android** (APK)

## Workflow Triggers

The workflow runs automatically on:
1. **Push to main branch**: Creates/updates a "latest" pre-release with development builds
2. **Push tags matching `v*`**: Creates a new release with the tag version (e.g., v1.0.0, v1.2.3)
3. **Manual trigger**: Can be triggered manually from the GitHub Actions tab

## Build Jobs

### 1. Windows Build
- **Platform**: Windows (x64)
- **Output**: Self-contained executable with all dependencies
- **Format**: ZIP archive containing Client.exe and required DLLs
- **File**: `HBArbitrageRobot-Client-Windows-x64.zip`

### 2. macOS Build
- **Platforms**: 
  - macOS x64 (Intel processors)
  - macOS ARM64 (Apple Silicon M1/M2/M3)
- **Output**: Self-contained executable with all dependencies
- **Format**: ZIP archives
- **Files**: 
  - `HBArbitrageRobot-Client-macOS-x64.zip`
  - `HBArbitrageRobot-Client-macOS-ARM64.zip`

### 3. Android Build
- **Platform**: Android (ARM64 and x64)
- **Output**: Android APK package
- **Format**: APK file
- **File**: `HBArbitrageRobot-Client-Android.apk`
- **Minimum Android Version**: Android 5.0 (API level 21)

## Installation Instructions

### Windows
1. Download `HBArbitrageRobot-Client-Windows-x64.zip`
2. Extract the ZIP file to a folder
3. Run `Client.exe`
4. Install Playwright browsers: Open PowerShell in the folder and run `.\playwright.ps1 install`

### macOS
1. Download the appropriate ZIP file for your Mac:
   - Intel Macs: `HBArbitrageRobot-Client-macOS-x64.zip`
   - Apple Silicon (M1/M2/M3): `HBArbitrageRobot-Client-macOS-ARM64.zip`
2. Extract the ZIP file
3. Make the app executable: `chmod +x Client`
4. Run: `./Client`
5. Install Playwright browsers: `pwsh playwright.ps1 install` (requires PowerShell)

**Note**: You may need to allow the app in System Preferences > Security & Privacy

### Android
1. Download `HBArbitrageRobot-Client-Android.apk`
2. Enable "Install from Unknown Sources" in Android Settings
3. Open the APK file to install
4. Grant necessary permissions when prompted

**Note**: The Android version requires internet access and may need additional permissions for automation features.

**Testing on Windows:** See [ANDROID_TESTING_WINDOWS.md](ANDROID_TESTING_WINDOWS.md) for detailed instructions on how to test the Android APK on Windows using emulators or physical devices.

## Release Types

### Tagged Releases (v*)
When you push a tag (e.g., `git tag v1.0.0 && git push origin v1.0.0`):
- Creates a new GitHub release with the tag version
- Includes all platform builds
- Marked as a stable release
- Release notes are auto-generated from commits

### Latest Development Build
When you push to the main branch:
- Updates the "latest" release
- Marked as pre-release (development build)
- Always contains the most recent main branch build
- Includes commit SHA and timestamp

## Architecture Details

### Project Configuration
The Client project (`Client.csproj`) is configured to support multiple target frameworks:
- `net10.0`: Desktop platforms (Windows, macOS, Linux)
- `net10.0-android`: Android platform (conditional)

The Android target is only enabled when building with the `BuildForAndroid=true` property to avoid requiring the Android workload for desktop-only development.

### Dependencies
- **Avalonia 11.1.3**: Cross-platform UI framework
- **Microsoft.Playwright 1.57.0**: Browser automation
- **SQLite-net-pcl**: Database storage
- **Newtonsoft.Json**: JSON serialization
- **Platform-specific packages**:
  - `Avalonia.Desktop` for Windows/macOS/Linux
  - `Avalonia.Android` for Android

### Android-Specific Details
- **Application ID**: `com.hbarbitragerobot.client`
- **Entry Point**: `MainActivity.cs` (Android Activity)
- **Minimum SDK**: Android 5.0 (API 21)
- **Target SDK**: Latest
- **Package Format**: APK (not AAB)

## Building Locally

### Desktop (Windows/macOS/Linux)
```bash
# Build
dotnet build -c Release

# Publish for Windows
dotnet publish -f net10.0 -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Publish for macOS (Intel)
dotnet publish -f net10.0 -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true

# Publish for macOS (Apple Silicon)
dotnet publish -f net10.0 -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

### Android
```bash
# Install Android workload (one-time)
dotnet workload install android

# Restore with Android support
dotnet restore /p:BuildForAndroid=true

# Build APK
dotnet publish -f net10.0-android -c Release /p:BuildForAndroid=true
```

## Troubleshooting

### Windows
- **Issue**: "Windows protected your PC" message
- **Solution**: Click "More info" → "Run anyway"

### macOS
- **Issue**: "App is damaged and can't be opened"
- **Solution**: Run `xattr -cr Client` in the folder, then try again

### Android
- **Issue**: APK won't install
- **Solution**: Enable "Unknown Sources" in Settings → Security

- **Issue**: App crashes on startup
- **Solution**: Ensure Android 5.0 or higher, check app permissions

## Workflow Maintenance

### Updating .NET Version
Edit the `DOTNET_VERSION` environment variable in `.github/workflows/build-release-client.yml`:
```yaml
env:
  DOTNET_VERSION: '10.0.x'  # Change to new version
```

### Updating Dependencies
Edit `Client/Client.csproj` and update package versions, then commit. The workflow will use the new versions automatically.

### Adding New Platforms
To add a new platform (e.g., Linux):
1. Add a new job in the workflow
2. Use appropriate `runs-on` (e.g., `ubuntu-latest`)
3. Use appropriate runtime identifier (e.g., `-r linux-x64`)
4. Add the artifact to the release step

## Security Notes

- All builds are created in isolated GitHub Actions runners
- No credentials or secrets are embedded in the builds
- APK is not signed with a production certificate (development use only)
- For production Android deployment, consider signing the APK with a proper keystore

## Support

For issues related to:
- **Workflow failures**: Check the Actions tab in GitHub
- **Build errors**: Review the build logs in failed workflow runs
- **Application bugs**: Create an issue in the repository
- **Platform-specific issues**: Refer to Avalonia documentation

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Avalonia Cross-Platform UI](https://avaloniaui.net/)
- [.NET Multi-Platform Support](https://docs.microsoft.com/dotnet/core/rid-catalog)
- [Android Development](https://developer.android.com/)
