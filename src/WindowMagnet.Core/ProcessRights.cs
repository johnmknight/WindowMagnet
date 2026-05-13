using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WindowMagnet.Core.Native;

namespace WindowMagnet.Core;

/// <summary>
/// Determines whether the current process has the rights to manipulate another
/// process's top-level windows. Windows blocks SetWindowPos across an integrity
/// boundary — a Medium-IL app (the default for any user-launched non-elevated
/// process) cannot move a window owned by a High-IL or System-IL process. This
/// shows up as <c>SetWindowPos</c> returning false with GetLastError = 5
/// (ACCESS_DENIED).
///
/// We compare the mandatory integrity level of the target process to our own.
/// If the target is strictly higher, we can't move it — surface that in the UI
/// so the user knows to launch WindowMagnet as admin if they need to.
/// </summary>
public static class ProcessRights
{
    // Cache by PID. The integrity level of a running process doesn't change, and
    // PID reuse within a single session is rare enough to ignore for v0.2.
    private static readonly ConcurrentDictionary<uint, bool> _cache = new();
    private static readonly Lazy<int> _ourIntegrity = new(() =>
        GetIntegrityLevel(NativeMethods.GetCurrentProcess()));

    /// <summary>The mandatory integrity level RID of the current process.</summary>
    public static int OwnIntegrityLevel => _ourIntegrity.Value;

    /// <summary>True if SetWindowPos against this hwnd is expected to succeed.</summary>
    public static bool CanMoveWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return false;
        return _cache.GetOrAdd(pid, ComputeCanMove);
    }

    /// <summary>Drop a cached entry — useful in tests or after a process exit.</summary>
    public static void InvalidatePid(uint pid) => _cache.TryRemove(pid, out _);

    private static bool ComputeCanMove(uint pid)
    {
        // If OpenProcess fails at QUERY_LIMITED_INFORMATION (the gentlest access right),
        // the process is almost certainly higher-IL than us — protected processes
        // (anti-malware, the secure desktop, etc.) also fail here.
        var h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return false;
        try
        {
            int targetIl = GetIntegrityLevel(h);
            if (targetIl < 0) return false;
            return targetIl <= OwnIntegrityLevel;
        }
        finally
        {
            NativeMethods.CloseHandle(h);
        }
    }

    private static int GetIntegrityLevel(IntPtr processHandle)
    {
        if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TOKEN_QUERY, out IntPtr token))
            return -1;
        try
        {
            // Two-call pattern: first call returns the required buffer size in `needed`.
            NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel,
                IntPtr.Zero, 0, out uint needed);
            if (needed == 0) return -1;

            IntPtr buf = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel,
                        buf, needed, out _))
                {
                    return -1;
                }

                // TOKEN_MANDATORY_LABEL starts with SID_AND_ATTRIBUTES; the SID pointer
                // is the very first field. The integrity RID is the SID's last sub-authority.
                IntPtr sid = Marshal.ReadIntPtr(buf);
                IntPtr countPtr = NativeMethods.GetSidSubAuthorityCount(sid);
                int count = Marshal.ReadByte(countPtr);
                if (count <= 0) return -1;

                IntPtr lastSubAuth = NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1));
                return Marshal.ReadInt32(lastSubAuth);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }
}
