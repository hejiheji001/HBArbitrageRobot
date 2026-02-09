# Android 16 KB Page Size Fix

## Problem

Android 16 will require native libraries to support 16 KB page sizes. The SkiaSharp library (version 2.88.9) used by Avalonia contains `libSkiaSharp.so` that does not have 16 KB page size alignment, which will cause compatibility issues with Android 16.

Error message:
```
Android 16 will require 16 KB page sizes, shared library 'libSkiaSharp.so' does not have a 16 KB page size. 
Please inform the authors of the NuGet package 'SkiaSharp.NativeAssets.Android' version '2.88.9' 
which contains 'runtimes/android-x64/native/libSkiaSharp.so'.
```

## Solution

The fix involves using the `zipalign` tool with the `-p` flag to realign the APK with 16 KB page size during the build process. This ensures that all native libraries (including `libSkiaSharp.so`) are properly aligned to meet Android 16's requirements.

### Changes Made

1. **GitHub Actions Workflow** (`.github/workflows/build-release-client.yml`):
   - Added a new step "Align APK for 16KB page size (Android 16 compatibility)" after the APK is built
   - This step uses the `zipalign` tool with `-p 16384` flag to realign the APK
   - The step also verifies the alignment was successful

### How It Works

The `zipalign` tool is part of the Android SDK build tools and is used to optimize APK files. The `-P 16` flag ensures that:

1. All uncompressed data within the APK is aligned on 16 KB boundaries
2. Native libraries like `libSkiaSharp.so` are properly aligned
3. The APK is compatible with Android 16's memory page size requirements

The command used is:
```bash
zipalign -f -P 16 4 input.apk output.apk
```

Where:
- `-f` forces overwriting of the output file
- `-P 16` specifies 16 KB page size alignment
- `4` is the base alignment in bytes (for 32-bit alignment)

### Verification

The workflow includes a verification step:
```bash
$ZIPALIGN -c -P 16 4 ./HBArbitrageRobot-Client-Android.apk
```

This checks that the APK is properly aligned and will fail the build if alignment is incorrect.

## References

- [Android Developer Guide: Support 16 KB page sizes](https://developer.android.com/guide/practices/page-sizes)
- [zipalign documentation](https://developer.android.com/tools/zipalign)

## Future Considerations

When SkiaSharp is updated to version 3.x or later (which natively supports 16 KB page sizes), this workaround may no longer be necessary. However, this fix ensures compatibility until then and has no negative impact on current Android versions.
