using ScreenLink.Server.Services;
using ScreenLink.Server.ViewModels;
using System.Windows;

namespace ScreenLink.Server;

public partial class MainWindow : Window
{
    private ScreenshotServer? _server;
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private async void btnStartServer_Click(object sender, RoutedEventArgs e)
    {
        if (_server != null)
        {
            try
            {
                await _server.StopAsync();
                _server.Dispose();
                _server = null;

                btnStartServer.Content = "Start Server";
                _viewModel.Status = "Server stopped";
                _viewModel.AddLog("Server stopped");
                _viewModel.Clients.Clear();
                return;
            }
            catch (Exception ex)
            {
                _viewModel.AddLog($"Error stopping server: {ex.Message}");
                return;
            }
        }

        if (!int.TryParse(_viewModel.Port, out int port))
        {
            MessageBox.Show("Invalid port number");
            return;
        }

        try
        {
            _server = new ScreenshotServer(port, _viewModel);
            // 서버 시작을 별도 태스크로 실행
            _ = Task.Run(async () =>
            {
                try
                {
                    await _server.StartAsync();
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _viewModel.AddLog($"Server error: {ex.Message}");
                        _viewModel.Status = "Server error";
                        btnStartServer.Content = "Start Server";
                    });
                }
            });

            btnStartServer.Content = "Stop Server";
            _viewModel.Status = $"Server running on port {port}";
            _viewModel.AddLog($"Server started on port {port}");
        }
        catch (Exception ex)
        {
            _viewModel.AddLog($"Error starting server: {ex.Message}");
            _viewModel.Status = "Failed to start server";
        }
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_server != null)
        {
            try
            {
                await _server.StopAsync();
                _server.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping server: {ex.Message}");
            }
        }
        base.OnClosing(e);
    }
}