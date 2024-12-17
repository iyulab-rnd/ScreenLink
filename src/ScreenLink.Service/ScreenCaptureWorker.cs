using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ScreenLink.Service;

public class ScreenCaptureWorker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private ScreenshotClient? _screenshotClient;
    private string _serverHost;
    private int _serverPort;
    private int _captureInterval;

    private readonly ILogger<ScreenCaptureWorker> _logger;
    private readonly ScreenCaptureManager _captureManager;

    public ScreenCaptureWorker(
        ILogger<ScreenCaptureWorker> logger,
        IConfiguration configuration,
        ScreenCaptureManager captureManager)
    {
        _logger = logger;
        _configuration = configuration;
        _captureManager = captureManager;
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            _serverHost = _configuration["ServerSettings:Host"] ?? throw new InvalidOperationException("ServerSettings:Host is not configured");
            _serverPort = int.Parse(_configuration["ServerSettings:Port"] ?? throw new InvalidOperationException("ServerSettings:Port is not configured"));
            _captureInterval = int.Parse(_configuration["CaptureSettings:IntervalMilliseconds"] ?? throw new InvalidOperationException("CaptureSettings:IntervalMilliseconds is not configured"));

            _logger.LogInformation("Configuration loaded successfully:");
            _logger.LogInformation("Server Host: {Host}", _serverHost);
            _logger.LogInformation("Server Port: {Port}", _serverPort);
            _logger.LogInformation("Capture Interval: {Interval}ms", _captureInterval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration. Using default values.");
            // 기본값 설정
            _serverHost = "59.6.212.19";
            _serverPort = 3000;
            _captureInterval = 1000;
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_serverHost))
        {
            throw new InvalidOperationException("Server host cannot be empty");
        }
        if (_serverPort <= 0 || _serverPort > 65535)
        {
            throw new InvalidOperationException($"Invalid port number: {_serverPort}");
        }
        if (_captureInterval < 100 || _captureInterval > 60000)
        {
            throw new InvalidOperationException($"Invalid capture interval: {_captureInterval}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting ScreenLink Service...");

            await Task.Delay(2000, stoppingToken); // 서비스 시작 시 약간의 지연 추가

            _screenshotClient = new ScreenshotClient(_serverHost, _serverPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Connecting to server {Host}:{Port}...", _serverHost, _serverPort);
                    await _screenshotClient.ConnectAsync();
                    _logger.LogInformation("Connected to server successfully");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var screenshot = _captureManager.CaptureScreen();
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                        _logger.LogInformation("Sending screenshot {Timestamp}", timestamp);
                        await _screenshotClient.SendScreenshotAsync(screenshot, timestamp);
                        _logger.LogInformation("Screenshot sent successfully");

                        await Task.Delay(_captureInterval, stoppingToken);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error in capture loop. Retrying in 5 seconds...");

                    _screenshotClient?.Dispose();
                    _screenshotClient = new ScreenshotClient(_serverHost, _serverPort);

                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping service...");

        _captureManager?.Dispose();
        _screenshotClient?.Dispose();

        await base.StopAsync(stoppingToken);
    }
}