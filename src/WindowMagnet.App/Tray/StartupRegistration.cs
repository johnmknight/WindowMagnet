using System;
using Microsoft.Win32;

namespace WindowMagnet.App.Tray;

/// <summary>
/// Reads / writes the per-user "run at login" key under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>. Per-user (HKCU)
/// rather than per-machine (HKLM) so no elevation is required and the toggle
/// in the tray menu can be flipped freely.
/// <para>
/// The value stored is the full path to the running WindowMagnet.exe (wrapped
/// in quotes to survive paths with spaces). Reinstall to a new location and
/// the toggle needs to be re-applied; otherwise self-healing is on the user.
/// </para>
/// </summary>
public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowMagnet";

    /// <summary>True if WindowMagnet is currently registered to run at login.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrEmpty(s);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Flip the registration on or off. Errors are logged and swallowed.</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            if (key is null) return;
            if (enabled)
            {
                var path = GetExePath();
                if (string.IsNullOrEmpty(path))
                {
                    App.Log("startup: couldn't determine exe path; leaving registry unchanged");
                    return;
                }
                // Quote the path so spaces survive (e.g. "C:\Program Files\WindowMagnet\...").
                key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
                App.Log($"startup: registered '{path}' for run-at-login");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                App.Log("startup: unregistered run-at-login");
            }
        }
        catch (Exception ex)
        {
            App.Log($"startup: registration toggle FAILED — {ex.Message}");
        }
    }

    private static string GetExePath()
    {
        // Environment.ProcessPath is the .NET 6+ way; falls back to MainModule for safety.
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)) return path;
        try
        {
            return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
        }
        catch
        {
            return "";
        }
    }
}
