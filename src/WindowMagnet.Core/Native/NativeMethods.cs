using System.Runtime.InteropServices;
using System.Text;

namespace WindowMagnet.Core.Native;

/// <summary>
/// P/Invoke surface for user32 and dwmapi. See DESIGN.md §3 for the rationale
/// behind each API choice.
/// </summary>
internal static class NativeMethods
{
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    // ---- user32 ----

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ---- dwmapi ----

    [DllImport("dwmapi.dll")]
    internal static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUnregisterThumbnail(IntPtr thumb);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out SIZE size);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

    // ---- kernel32 / advapi32 (integrity checks) ----

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("advapi32.dll")]
    internal static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);

    [DllImport("advapi32.dll")]
    internal static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

    // Process / token access rights
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint TOKEN_QUERY = 0x0008;

    // TOKEN_INFORMATION_CLASS.TokenIntegrityLevel
    internal const int TokenIntegrityLevel = 25;

    // Standard mandatory integrity level RIDs (the last sub-authority of the integrity SID).
    // A process at level N cannot SetWindowPos a window owned by a process at level > N.
    internal const int SECURITY_MANDATORY_UNTRUSTED_RID = 0x00000000;
    internal const int SECURITY_MANDATORY_LOW_RID       = 0x00001000;
    internal const int SECURITY_MANDATORY_MEDIUM_RID    = 0x00002000;
    internal const int SECURITY_MANDATORY_HIGH_RID      = 0x00003000;
    internal const int SECURITY_MANDATORY_SYSTEM_RID    = 0x00004000;

    // ---- constants ----

    internal const uint SWP_NOSIZE         = 0x0001;
    internal const uint SWP_NOZORDER       = 0x0004;
    internal const uint SWP_NOACTIVATE     = 0x0010;
    internal const uint SWP_SHOWWINDOW     = 0x0040;
    internal const uint SWP_ASYNCWINDOWPOS = 0x4000;

    internal const uint MONITORINFOF_PRIMARY = 0x00000001;

    internal const int SW_SHOWNOACTIVATE = 4;
    internal const int SW_RESTORE        = 9;

    internal const uint DWM_TNP_RECTDESTINATION     = 0x00000001;
    internal const uint DWM_TNP_RECTSOURCE          = 0x00000002;
    internal const uint DWM_TNP_OPACITY             = 0x00000004;
    internal const uint DWM_TNP_VISIBLE             = 0x00000008;
    internal const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    internal const uint DWMWA_CLOAKED = 14;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left, Top, Right, Bottom;
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE
{
    public int cx, cy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DWM_THUMBNAIL_PROPERTIES
{
    public uint dwFlags;
    public RECT rcDestination;
    public RECT rcSource;
    public byte opacity;
    [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
    [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;
}
