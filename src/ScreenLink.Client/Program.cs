using Microsoft.Extensions.Configuration;
using ScreenLink.Client;

public class Program
{
    private static string _serverHost = "localhost";
    private static int _serverPort = 5001;
    private static int _captureInterval = 200;
    private static ScreenCaptureManager? _captureManager;
    private static ScreenshotClient? _screenshotClient;

    static async Task Main(string[] args)
    {
        try
        {
            var firewallManager = new FirewallManager();
            firewallManager.AddFirewallRules();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to add firewall rules: {ex.Message}");
            Console.WriteLine("Application will continue, but may have network access issues.");
        }

        LoadConfiguration();

        _captureManager = new ScreenCaptureManager();
        _screenshotClient = new ScreenshotClient(_serverHost, _serverPort);

        Console.WriteLine($"Connecting to server: {_serverHost}:{_serverPort}");
        Console.WriteLine($"Screenshot interval: {_captureInterval}ms");

        using var cts = new CancellationTokenSource();

        _ = Task.Run(() =>
        {
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        try
        {
            await CaptureAndSendScreenshots(cts.Token);
        }
        finally
        {
            _captureManager.Dispose();
            _screenshotClient.Dispose();
        }
    }

    private static async Task CaptureAndSendScreenshots(CancellationToken cancellationToken)
    {
        int retryCount = 0;
        const int MAX_RETRY_DELAY = 10000;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _screenshotClient!.ConnectAsync();
                Console.WriteLine("Connected to server");
                retryCount = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var screenshot = _captureManager!.CaptureScreen();
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    await _screenshotClient.SendScreenshotAsync(screenshot, timestamp);
                    Console.WriteLine($"Screenshot sent at: {timestamp}");
                    await Task.Delay(_captureInterval, cancellationToken);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                retryCount++;
                int delayMs = Math.Min(1000 * (int)Math.Pow(2, retryCount - 1), MAX_RETRY_DELAY);

                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Connection attempt {retryCount} failed. Retrying in {delayMs / 1000} seconds...");

                try
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                Console.WriteLine(new string('-', 50));
            }
        }
    }

    private static void LoadConfiguration()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        _serverHost = config.GetValue<string>("ServerSettings:Host") ?? "localhost";
        _serverPort = config.GetValue<int>("ServerSettings:Port", 5001);
        _captureInterval = config.GetValue<int>("CaptureSettings:IntervalMilliseconds", 200); // Default changed to 200ms for 5 FPS
    }
}