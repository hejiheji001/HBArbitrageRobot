using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Client.Services;
using System;

namespace Client;

[Activity(
    Label = "HBArbitrageRobot",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        // Setup crash handler
        SetupCrashHandler();
        
        // Initialize logging
        var logger = LoggingService.Instance;
        logger.LogInfo("MainActivity OnCreate - App starting");
        
        base.OnCreate(savedInstanceState);
    }

    private void SetupCrashHandler()
    {
        // Setup unhandled exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            var logger = LoggingService.Instance;
            logger.LogError("Unhandled exception occurred", exception);
            
            // Also log to Android logcat
            Android.Util.Log.Error("HBArbitrageRobot", $"Unhandled exception: {exception?.Message}");
            Android.Util.Log.Error("HBArbitrageRobot", $"StackTrace: {exception?.StackTrace}");
        };

        // Setup task scheduler unobserved task exception handler
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            var logger = LoggingService.Instance;
            logger.LogError("Unobserved task exception", args.Exception);
            args.SetObserved(); // Prevent the app from crashing
            
            // Also log to Android logcat
            Android.Util.Log.Error("HBArbitrageRobot", $"Unobserved task exception: {args.Exception.Message}");
        };
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
