# Deploy Android APK to Connected Phone
# Quick deployment script for HBArbitrageRobot Android app
# Usage: .\deploy-android.ps1

param(
    [switch]$Release,
    [switch]$Launch,
    [switch]$ShowLogs
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Android Phone Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$Config = if ($Release) { "Release" } else { "Debug" }
$PackageId = "com.hbarbitragerobot.client"
$ActivityName = ".MainActivity"

# Check for connected devices
Write-Host "Checking for connected Android devices..." -ForegroundColor Yellow
$devicesOutput = adb devices 2>&1
$devices = $devicesOutput | Select-String "device$" | Where-Object { $_ -notmatch "List of devices" }

if ($devices.Count -eq 0) {
    Write-Host ""
    Write-Host "ERROR: No Android devices connected!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure:" -ForegroundColor Yellow
    Write-Host "  1. Your phone is connected via USB" -ForegroundColor Yellow
    Write-Host "  2. USB debugging is enabled on your phone" -ForegroundColor Yellow
    Write-Host "  3. You've authorized this computer on your phone" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To check devices, run: adb devices" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host "✓ Found connected device(s):" -ForegroundColor Green
$devices | ForEach-Object {
    Write-Host "  $($_ -replace '\s+device$', '')" -ForegroundColor Gray
}
Write-Host ""

# Navigate to Client directory
$ClientPath = Join-Path $PSScriptRoot "Client"
if (-not (Test-Path $ClientPath)) {
    Write-Host "ERROR: Client directory not found!" -ForegroundColor Red
    Write-Host "Expected: $ClientPath" -ForegroundColor Yellow
    exit 1
}

Push-Location $ClientPath

try {
    # Build
    Write-Host "Building Android APK ($Config configuration)..." -ForegroundColor Yellow
    Write-Host ""
    
    dotnet build -f net10.0-android -c $Config /p:BuildForAndroid=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: Build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "✓ Build successful" -ForegroundColor Green
    Write-Host ""
    
    # Publish
    Write-Host "Publishing APK..." -ForegroundColor Yellow
    Write-Host ""
    
    dotnet publish -f net10.0-android -c $Config /p:BuildForAndroid=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: Publish failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "✓ Publish successful" -ForegroundColor Green
    Write-Host ""
    
    # Find APK
    $publishPath = "bin\$Config\net10.0-android\publish"
    $apkFile = Get-ChildItem -Path $publishPath -Filter "*.apk" -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if ($null -eq $apkFile) {
        Write-Host "ERROR: APK file not found in $publishPath!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Found APK: $($apkFile.Name)" -ForegroundColor Gray
    Write-Host "Size: $([math]::Round($apkFile.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host ""
    
    # Check if app is already installed
    Write-Host "Checking for existing installation..." -ForegroundColor Yellow
    $installed = adb shell pm list packages | Select-String $PackageId
    
    if ($installed) {
        Write-Host "⚠ App is already installed. Reinstalling..." -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Install
    Write-Host "Installing APK to device..." -ForegroundColor Yellow
    Write-Host "(This may take 30-60 seconds)" -ForegroundColor Gray
    Write-Host ""
    
    adb install -r $apkFile.FullName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: Installation failed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Try these solutions:" -ForegroundColor Yellow
        Write-Host "  1. Uninstall the app from your phone manually" -ForegroundColor Yellow
        Write-Host "  2. Run: adb uninstall $PackageId" -ForegroundColor Yellow
        Write-Host "  3. Free up storage space on your phone" -ForegroundColor Yellow
        Write-Host "  4. Try a different USB port or cable" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host ""
    Write-Host "✓ Installation successful" -ForegroundColor Green
    Write-Host ""
    
    # Launch app
    if ($Launch) {
        Write-Host "Launching app..." -ForegroundColor Yellow
        adb shell am start -n "$PackageId/$ActivityName"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ App launched" -ForegroundColor Green
            Write-Host ""
        }
    }
    
    # Show logs
    if ($ShowLogs) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Watching app logs (Ctrl+C to exit)..." -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        
        # Clear existing logs
        adb logcat -c
        
        # Show logs with filtering
        adb logcat | Select-String "HBArbitrageRobot|AndroidRuntime"
    }
    else {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "✓ Deployment completed successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "The app is now installed on your device." -ForegroundColor White
        Write-Host ""
        Write-Host "Useful commands:" -ForegroundColor Cyan
        Write-Host "  Launch app:" -ForegroundColor Yellow
        Write-Host "    adb shell am start -n $PackageId/$ActivityName" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  View logs:" -ForegroundColor Yellow
        Write-Host "    adb logcat | Select-String 'HBArbitrageRobot'" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Stop app:" -ForegroundColor Yellow
        Write-Host "    adb shell am force-stop $PackageId" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Uninstall:" -ForegroundColor Yellow
        Write-Host "    adb uninstall $PackageId" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Next time, use flags for convenience:" -ForegroundColor Cyan
        Write-Host "  .\deploy-android.ps1 -Launch      # Deploy and launch" -ForegroundColor Gray
        Write-Host "  .\deploy-android.ps1 -ShowLogs    # Deploy and show logs" -ForegroundColor Gray
        Write-Host "  .\deploy-android.ps1 -Release     # Deploy release build" -ForegroundColor Gray
        Write-Host ""
    }
}
finally {
    Pop-Location
}
