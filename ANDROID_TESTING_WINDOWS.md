# Testing Android APK on Windows

This guide explains how to test the HBArbitrageRobot Android APK on your Windows machine.

## Testing Options

There are several ways to test Android apps on Windows:

1. **Windows Subsystem for Android (WSA)** - Native Windows 11 feature (Recommended for Windows 11)
2. **Android Studio Emulator** - Official Android emulator (Best for development)
3. **Physical Android Device** - Real device via USB (Most accurate testing)
4. **Third-Party Emulators** - BlueStacks, NoxPlayer, etc. (Easiest setup)

---

## Option 1: Windows Subsystem for Android (WSA) - Windows 11 Only

**Requirements:**
- Windows 11 (Build 22000 or higher)
- 8GB RAM minimum (16GB recommended)
- Virtualization enabled in BIOS
- Amazon Appstore app

### Setup Steps

#### 1. Install Windows Subsystem for Android

```powershell
# Check Windows version (must be 22000 or higher)
winver

# Open PowerShell as Administrator and run:
# Enable Virtual Machine Platform
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart

# Enable Windows Hypervisor Platform
dism.exe /online /enable-feature /featurename:HypervisorPlatform /all /norestart

# Restart your computer
shutdown /r /t 0
```

After restart:
1. Open Microsoft Store
2. Search for "Amazon Appstore"
3. Install the Amazon Appstore
4. Launch Amazon Appstore - this will install Windows Subsystem for Android

#### 2. Enable Developer Mode

1. Open **Settings** → **Privacy & Security** → **For developers**
2. Enable **Developer Mode**
3. Open **Windows Subsystem for Android Settings** from Start Menu
4. Turn on **Developer mode**
5. Note the IP address shown (e.g., 127.0.0.1:58526)

#### 3. Install ADB Tools

Download and install Android Debug Bridge:

```powershell
# Option A: Download ADB from Google
# Visit: https://developer.android.com/tools/releases/platform-tools
# Extract platform-tools folder to C:\adb

# Option B: Use Chocolatey
choco install adb

# Add to PATH
$env:Path += ";C:\adb\platform-tools"
```

#### 4. Connect to WSA

```powershell
# Connect ADB to WSA (use the IP from WSA Settings)
adb connect 127.0.0.1:58526

# Verify connection
adb devices
# Should show: 127.0.0.1:58526   device
```

#### 5. Install and Test APK

```powershell
# Navigate to where you downloaded the APK
cd Downloads

# Install the APK
adb install HBArbitrageRobot-Client-Android.apk

# Launch the app
adb shell am start -n com.hbarbitragerobot.client/.MainActivity

# View logs (optional, for debugging)
adb logcat | Select-String "HBArbitrageRobot"
```

#### 6. Using the App

1. Open Start Menu
2. Look for "HBArbitrageRobot" in the app list
3. Click to launch
4. The app will run in a window on Windows

### Troubleshooting WSA

**Issue: "adb: failed to install"**
```powershell
# Uninstall previous version
adb uninstall com.hbarbitragerobot.client

# Try installing again
adb install HBArbitrageRobot-Client-Android.apk
```

**Issue: WSA won't start**
- Ensure virtualization is enabled in BIOS
- Check Windows Update for latest WSA version
- Restart "Windows Subsystem for Android" service

**Issue: Can't connect with ADB**
- Restart WSA from Settings
- Check firewall isn't blocking port 58526
- Try rebooting Windows

---

## Option 2: Android Studio Emulator (Recommended for Developers)

**Best for:** Developers who need full Android debugging capabilities

### Setup Steps

#### 1. Install Android Studio

1. Download from: https://developer.android.com/studio
2. Run installer and follow prompts
3. Launch Android Studio

#### 2. Setup Android Emulator

1. Open Android Studio
2. Click **Tools** → **Device Manager**
3. Click **Create Device**
4. Select a device (e.g., "Pixel 7")
5. Select a system image (e.g., "Android 13 (API 33)")
   - Click **Download** if not installed
6. Click **Finish**

#### 3. Start Emulator

In Android Studio:
1. Open **Device Manager**
2. Click ▶️ (Play) button next to your virtual device
3. Wait for emulator to boot (may take 1-2 minutes first time)

#### 4. Install APK

**Method A: Drag and Drop**
1. Simply drag `HBArbitrageRobot-Client-Android.apk` onto the emulator window
2. Wait for installation to complete
3. App appears in app drawer

**Method B: Using ADB**
```powershell
# Make sure emulator is running
adb devices

# Install APK
adb install HBArbitrageRobot-Client-Android.apk

# Launch app
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
```

#### 5. View Logs (Debugging)

In Android Studio:
1. Click **Logcat** tab at bottom
2. Select your emulator from device dropdown
3. Filter for "HBArbitrageRobot" to see app logs

### Emulator Tips

- **Faster startup:** Use "Quick Boot" feature
- **Performance:** Allocate more RAM in AVD settings (4GB recommended)
- **Internet:** Emulator uses host machine's network connection
- **Screen rotation:** Ctrl + F11/F12
- **Back button:** Esc key

---

## Option 3: Physical Android Device (Most Accurate)

**Best for:** Testing on real hardware

### Setup Steps

#### 1. Enable Developer Options on Android Device

1. Open **Settings** on your Android device
2. Go to **About Phone**
3. Tap **Build Number** 7 times
4. You'll see "You are now a developer!"

#### 2. Enable USB Debugging

1. Go to **Settings** → **Developer Options**
2. Enable **USB Debugging**
3. Enable **Install via USB** (if available)

#### 3. Install ADB on Windows

```powershell
# Download platform-tools from Google
# https://developer.android.com/tools/releases/platform-tools
# Extract to C:\adb

# Or use Chocolatey
choco install adb

# Add to PATH
$env:Path += ";C:\adb\platform-tools"
```

#### 4. Connect Device

1. Connect Android device to Windows PC via USB cable
2. On Android device, authorize the computer when prompted
3. Check connection:

```powershell
adb devices
# Should show your device serial number
```

#### 5. Install APK

```powershell
# Install
adb install HBArbitrageRobot-Client-Android.apk

# Launch
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
```

#### 6. Alternative: Direct Install

1. Copy APK file to your phone (via USB, email, or cloud)
2. Open file manager on Android
3. Navigate to the APK file
4. Tap to install
5. Allow "Install from Unknown Sources" if prompted

### Wireless Debugging (Android 11+)

```powershell
# On Android device:
# Settings → Developer Options → Wireless Debugging → Pair device with pairing code

# On Windows:
adb pair <IP:PORT>
# Enter pairing code from device

adb connect <IP:PORT>
# Now you can install APKs wirelessly
```

---

## Option 4: Third-Party Emulators (Easiest Setup)

**Best for:** Quick testing without Android Studio

### BlueStacks (Recommended)

1. **Download:** https://www.bluestacks.com/
2. **Install:** Run installer and follow prompts
3. **Launch:** Start BlueStacks
4. **Install APK:**
   - Click **Install APK** button in BlueStacks
   - Select `HBArbitrageRobot-Client-Android.apk`
   - Wait for installation
   - App appears on home screen

### NoxPlayer

1. **Download:** https://www.bignox.com/
2. **Install and launch**
3. **Install APK:**
   - Drag APK file into NoxPlayer window
   - Or use **Add APK** button

### MEmu Play

1. **Download:** https://www.memuplay.com/
2. **Install and launch**
3. **Install APK:**
   - Click **APK** button in sidebar
   - Select APK file

### Pros and Cons of Third-Party Emulators

**Pros:**
- Easy to install and use
- Good performance
- No development tools needed

**Cons:**
- Less accurate than real device
- May include ads or bloatware
- Less control over Android version

---

## Testing the App

Once installed, test these features:

### 1. Basic Launch
- App opens without crashing
- UI is visible and responsive

### 2. Configuration
- Can enter username, password
- Can set API URL
- Settings are saved

### 3. Network Access
- App can connect to server API
- Data fetches successfully

### 4. Permissions
Check if app requests necessary permissions:
- Internet access
- Storage (for database)

### 5. Background Execution
- App continues running when minimized
- Scheduled tasks work correctly

---

## Debugging

### View App Logs

```powershell
# Real-time logs
adb logcat | Select-String "HBArbitrageRobot"

# Save logs to file
adb logcat -d > app-logs.txt

# Clear logs and restart
adb logcat -c
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
adb logcat | Select-String "HBArbitrageRobot"
```

### Common Issues

**App crashes on startup:**
```powershell
# Check crash logs
adb logcat -d | Select-String "AndroidRuntime"

# Reinstall
adb uninstall com.hbarbitragerobot.client
adb install HBArbitrageRobot-Client-Android.apk
```

**Can't connect to API:**
- Check internet connection
- Verify API URL is correct
- Check firewall settings
- Use emulator's actual IP, not localhost

**App not appearing after install:**
```powershell
# Check if installed
adb shell pm list packages | Select-String "hbarbitragerobot"

# Launch manually
adb shell am start -n com.hbarbitragerobot.client/.MainActivity
```

### Uninstall App

```powershell
adb uninstall com.hbarbitragerobot.client
```

---

## Building APK Locally for Testing

If you want to build the APK yourself instead of using the workflow:

```powershell
# Navigate to project root
cd HBArbitrageRobot

# Install Android workload (first time only)
dotnet workload install android

# Build APK
cd Client
dotnet publish -f net10.0-android -c Release /p:BuildForAndroid=true

# APK location
# Client\bin\Release\net10.0-android\publish\com.hbarbitragerobot.client-Signed.apk

# Install to device/emulator
adb install bin\Release\net10.0-android\publish\*.apk
```

---

## Performance Tips

### For Emulators
- Allocate at least 4GB RAM
- Enable hardware acceleration
- Use x86/x64 images for better performance
- Close other applications

### For Physical Devices
- Use Android 8.0 or higher for best compatibility
- Ensure stable USB connection
- Keep device charged during testing

---

## Comparison of Testing Options

| Feature | WSA (Win11) | Android Studio | Physical Device | BlueStacks |
|---------|-------------|----------------|-----------------|------------|
| Setup Difficulty | Medium | Hard | Easy | Very Easy |
| Performance | Excellent | Good | Excellent | Good |
| Accuracy | Good | Good | Best | Fair |
| Debugging | Good | Excellent | Good | Limited |
| Cost | Free | Free | Device cost | Free |
| Best For | Win11 users | Developers | Final testing | Quick testing |

---

## Recommended Testing Workflow

1. **Initial Development:** Android Studio Emulator (full debugging)
2. **Quick Testing:** BlueStacks or WSA (fast iterations)
3. **Final Validation:** Physical Device (real-world testing)

---

## Additional Resources

- [Android Debug Bridge (ADB) Documentation](https://developer.android.com/tools/adb)
- [Windows Subsystem for Android Documentation](https://learn.microsoft.com/en-us/windows/android/wsa/)
- [Android Studio Guide](https://developer.android.com/studio/intro)
- [Avalonia Android Documentation](https://docs.avaloniaui.net/docs/next/guides/platforms/android)

---

## Support

If you encounter issues:
1. Check logs with `adb logcat`
2. Verify Android version is 5.0 or higher
3. Ensure all permissions are granted
4. Try reinstalling the app
5. Check GitHub Issues for known problems
