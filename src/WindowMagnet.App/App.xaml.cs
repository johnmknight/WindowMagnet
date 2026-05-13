using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WindowMagnet.App;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowMagnet", "windowmagnet.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // %APPDATA%\WindowMagnet — home for profiles.json and logs.
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        // Catch any unhandled exception so it lands in the log instead of vanishing.
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;

        Log($"=== startup pid={Environment.ProcessId} ver=0.1 ===");
        base.OnStartup(e);
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
