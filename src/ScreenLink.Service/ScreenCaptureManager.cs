using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenLink.Service;

public class ScreenCaptureManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, [Out] byte[] pvInfo, int nLength, out int lpnLengthNeeded);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr Token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr ExistingTokenHandle, uint dwDesiredAccess,
        IntPtr lpTokenAttributes, int ImpersonationLevel, int TokenType, out IntPtr DuplicateTokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    private bool disposed = false;
    private readonly string captureFolder;
    private readonly ILogger<ScreenCaptureManager> _logger;

    public ScreenCaptureManager(ILogger<ScreenCaptureManager> logger)
    {
        _logger = logger;
        SetProcessDPIAware();

        captureFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Captures");
        EnsureCaptureFolder();

        // Session 0 격리 확인
        if (IsRunningInSession0())
        {
            _logger.LogWarning("Running in Session 0. Screen capture may not work properly.");
        }
    }

    private bool IsRunningInSession0()
    {
        try
        {
            var sessionId = WTSGetActiveConsoleSessionId();
            return Process.GetCurrentProcess().SessionId == 0 && sessionId != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check session status");
            return false;
        }
    }

    public byte[] CaptureScreen()
    {
        if (IsRunningInSession0())
        {
            return CaptureScreenAsUser();
        }

        return CaptureScreenDirect();
    }

    private byte[] CaptureScreenAsUser()
    {
        try
        {
            var sessionId = WTSGetActiveConsoleSessionId();
            if (!WTSQueryUserToken(sessionId, out IntPtr token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                const uint MAXIMUM_ALLOWED = 0x02000000;
                if (!DuplicateTokenEx(token, MAXIMUM_ALLOWED, IntPtr.Zero, 2, 1, out IntPtr duplicateToken))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    var startInfo = new STARTUPINFO();
                    startInfo.cb = Marshal.SizeOf(startInfo);

                    var processInfo = new PROCESS_INFORMATION();

                    var currentPath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (currentPath == null) throw new InvalidOperationException("Cannot get current process path");

                    if (!CreateProcessAsUser(
                        duplicateToken,
                        currentPath,
                        "--capture",
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        0,
                        IntPtr.Zero,
                        null,
                        ref startInfo,
                        out processInfo))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    // Wait for the process to complete and get the result
                    using var process = Process.GetProcessById((int)processInfo.dwProcessId);
                    process.WaitForExit();

                    var captureFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_capture.png");
                    if (File.Exists(captureFile))
                    {
                        var result = File.ReadAllBytes(captureFile);
                        File.Delete(captureFile);
                        return result;
                    }

                    return Array.Empty<byte>();
                }
                finally
                {
                    Marshal.FreeHGlobal(duplicateToken);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screen as user");
            return Array.Empty<byte>();
        }
    }

    public byte[] CaptureScreenDirect()
    {
        var screen = Screen.PrimaryScreen!;
        var bounds = screen.Bounds;

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.CopyFromScreen(
            sourceX: bounds.X,
            sourceY: bounds.Y,
            destinationX: 0,
            destinationY: 0,
            bounds.Size);

        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        return memory.ToArray();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern IntPtr GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const int MDT_EFFECTIVE_DPI = 0;
    
    private void EnsureCaptureFolder()
    {
        try
        {
            if (!Directory.Exists(captureFolder))
            {
                Directory.CreateDirectory(captureFolder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create capture folder: {ex.Message}");
            throw;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Console.WriteLine("Display settings changed. Updating screen capture settings...");
        UpdateScreenCaptureDimensions();
    }

    private void UpdateScreenCaptureDimensions()
    {
        var screen = Screen.PrimaryScreen!;
        var bounds = screen.Bounds;

        IntPtr monitor = MonitorFromWindow(IntPtr.Zero, 0x00000002);
        GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);

        Console.WriteLine($"Updated Screen Info:");
        Console.WriteLine($"Bounds: {bounds.Width}x{bounds.Height}");
        Console.WriteLine($"DPI: {dpiX}x{dpiY}");
        Console.WriteLine($"Scale Factor: {dpiX / 96.0f}x{dpiY / 96.0f}");
    }

    public void Dispose()
    {
        if (!disposed)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            disposed = true;
        }
    }
}