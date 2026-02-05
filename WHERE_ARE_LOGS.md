# Quick Reference: Where to Find Logs

## Android App Crashed? Here's Where to Find Logs!

### Method 1: Using the App (Easiest) ⭐

1. **Open the HBArbitrageRobot app**
2. **Tap the Settings tab** (bottom navigation)
3. **Tap "View Logs"** to see recent logs
4. **Tap "Share Logs"** to export all logs

**Exported log location:**
```
/sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt
```

### Method 2: Using ADB (For Developers)

```bash
# View real-time logs
adb logcat | grep "HBArbitrageRobot"

# Pull exported logs
adb pull /sdcard/Android/data/com.hbarbitragerobot.client/files/HBArbitrageRobot_logs.txt
```

### Method 3: Using File Manager

1. Open your file manager app
2. Navigate to: `Android/data/com.hbarbitragerobot.client/files/`
3. Find: `HBArbitrageRobot_logs.txt`
4. Open with any text viewer

---

## Full Documentation

For detailed information, see:
- **[LOG_COLLECTION_GUIDE.md](LOG_COLLECTION_GUIDE.md)** - Complete guide
- **[ANDROID_PHONE_DEBUG.md](ANDROID_PHONE_DEBUG.md)** - Debugging guide

## Quick Tips

✅ Logs are automatically captured when the app crashes
✅ Up to 5 log files are kept (25MB total)
✅ No passwords or sensitive data in logs
✅ Share logs when reporting issues
