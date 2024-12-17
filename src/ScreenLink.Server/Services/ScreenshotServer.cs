using MessagePack;
using ScreenLink.Common;
using ScreenLink.Server.ViewModels;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Windows.Media.Imaging;

namespace ScreenLink.Server.Services;

public class ScreenshotServer : IDisposable
{
    private readonly int _port;
    private readonly MainViewModel _viewModel;
    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts;
    private readonly List<Task> _clientTasks;
    private bool _disposed;

    public ScreenshotServer(int port, MainViewModel viewModel)
    {
        _port = port;
        _viewModel = viewModel;
        _cts = new CancellationTokenSource();
        _clientTasks = new List<Task>();
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                var clientTask = HandleClientAsync(client);
                _clientTasks.Add(clientTask);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        var ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
        var lengthBuffer = new byte[4];

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // 메시지 길이 읽기
                await ReadExactAsync(stream, lengthBuffer, 0, 4);
                var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                // 메시지 본문 읽기
                var messageBuffer = new byte[messageLength];
                await ReadExactAsync(stream, messageBuffer, 0, messageLength);

                // MessagePack으로 역직렬화
                var message = MessagePackSerializer.Deserialize<ScreenshotMessage>(messageBuffer);

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    _viewModel.UpdateClient(ipAddress, message.ImageData);
                    if (_viewModel.SelectedClient?.IpAddress == ipAddress)
                    {
                        UpdateScreenshotImage(message.ImageData);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                _viewModel.AddLog($"Client disconnected: {ipAddress}. Error: {ex.Message}");
            });
        }
        finally
        {
            client.Dispose();
        }
    }

    // 정확한 바이트 수를 읽기 위한 헬퍼 메서드
    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset + totalBytesRead, count - totalBytesRead));
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Connection closed by remote host");
            }
            totalBytesRead += bytesRead;
        }
    }

    private void UpdateScreenshotImage(byte[] imageData)
    {
        var mainWindow = App.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        if (mainWindow != null)
        {
            try
            {
                using var stream = new MemoryStream(imageData);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.DecodePixelWidth = 0;
                image.DecodePixelHeight = 0;
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.EndInit();
                image.Freeze();

                mainWindow.screenshotImage.Source = image;
            }
            catch (Exception ex)
            {
                _viewModel.AddLog($"Error updating screenshot: {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        _listener?.Stop();
        await Task.WhenAll(_clientTasks);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Dispose();
            _listener?.Stop();
            _disposed = true;
        }
    }
}