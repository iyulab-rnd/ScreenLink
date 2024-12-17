using MessagePack;
using ScreenLink.Common;
using System.Net.Sockets;

namespace ScreenLink.Service;

public class ScreenshotClient : IDisposable
{
    private readonly string _serverHost;
    private readonly int _serverPort;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _disposed;

    public ScreenshotClient(string host, int port)
    {
        _serverHost = host;
        _serverPort = port;
    }

    public async Task ConnectAsync()
    {
        _client = new TcpClient();
        await _client.ConnectAsync(_serverHost, _serverPort);
        _stream = _client.GetStream();
    }

    public async Task SendScreenshotAsync(byte[] imageData, string timestamp)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected to server");

        var message = new ScreenshotMessage
        {
            Timestamp = timestamp,
            ImageData = imageData
        };

        var serializedData = MessagePackSerializer.Serialize(message);
        var length = BitConverter.GetBytes(serializedData.Length);

        await _stream.WriteAsync(length);
        await _stream.WriteAsync(serializedData);
        await _stream.FlushAsync();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Dispose();
            _client?.Dispose();
            _disposed = true;
        }
    }
}