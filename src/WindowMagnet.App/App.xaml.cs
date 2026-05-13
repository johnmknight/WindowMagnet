using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WindowMagnet.App.Tray;

namespace WindowMagnet.App;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowMagnet", "windowmagnet.log");

    /// <summary>Single tray controller for the app's lifetime.</summary>
    public static TrayController? Tray { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // %APPDATA%\WindowMagnet — home for profiles.json and logs.
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        // Catch any unhandled exception so it lands in the log instead of vanishing.
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;

        Log($"=== startup pid={Environment.ProcessId} ver=0.3 ===");

        // ShutdownMode = OnExplicitShutdown so closing the picker hides to tray
        // instead of terminating the process. The tray menu's Quit calls Shutdown
        // explicitly when the user really wants out.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Tray = new TrayController();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Tray?.Dispose();
        Tray = null;
        base.OnExit(e);
    }

    private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"UNHANDLED dispatcher: {e.Exception}");
        // Don't terminate — let the user see what's wrong via the log.
        e.Handled = true;
    }

    private void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        Log($"UNHANDLED appdomain (terminating={e.IsTerminating}): {e.ExceptionObject}");
    }

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // logging must never throw
        }
    }
}
