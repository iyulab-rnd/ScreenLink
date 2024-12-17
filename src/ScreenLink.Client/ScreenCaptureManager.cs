using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

class ScreenCaptureManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern IntPtr GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const int MDT_EFFECTIVE_DPI = 0;
    private bool disposed = false;
    private int currentImageIndex = 1;
    private const int MAX_IMAGES = 10;
    private readonly string captureFolder;

    public ScreenCaptureManager()
    {
        SetProcessDPIAware();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Forms.Application.AddMessageFilter(new MonitorMessageFilter());

        // 캡처 저장 폴더 설정
        captureFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Captures");
        EnsureCaptureFolder();
    }

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

    private void CleanupOldCaptures()
    {
        try
        {
            var files = Directory.GetFiles(captureFolder, "*.png")
                                .OrderBy(f => File.GetCreationTime(f))
                                .ToList();

            while (files.Count >= MAX_IMAGES)
            {
                var oldestFile = files.First();
                File.Delete(oldestFile);
                files.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to cleanup old captures: {ex.Message}");
        }
    }

    public byte[] CaptureScreen()
    {
        var screen = Forms.Screen.PrimaryScreen!;
        var bounds = screen.Bounds;

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        graphics.CopyFromScreen(
            sourceX: bounds.X,
            sourceY: bounds.Y,
            destinationX: 0,
            destinationY: 0,
            bounds.Size);

        // 이미지 파일로 저장
        CleanupOldCaptures();
        var filename = Path.Combine(captureFolder, $"{currentImageIndex}.png");
        bitmap.Save(filename, ImageFormat.Png);

        Console.WriteLine($"Saved capture to: {filename}");

        // 다음 인덱스 준비 (1-10 순환)
        currentImageIndex = (currentImageIndex % MAX_IMAGES) + 1;

        // 메모리 스트림으로 변환하여 반환
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        return memory.ToArray();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Console.WriteLine("Display settings changed. Updating screen capture settings...");
        UpdateScreenCaptureDimensions();
    }

    private void UpdateScreenCaptureDimensions()
    {
        var screen = Forms.Screen.PrimaryScreen!;
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