using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using ScreenLink.Server.Models;

namespace ScreenLink.Server.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ClientInfo> clients;

    [ObservableProperty]
    private ObservableCollection<string> logs;

    [ObservableProperty]
    private ClientInfo selectedClient;

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private string port = "5001";

    public MainViewModel()
    {
        Clients = new ObservableCollection<ClientInfo>();
        Logs = new ObservableCollection<string>();
    }

    public void UpdateClient(string ipAddress, byte[] screenshot)
    {
        var client = Clients.FirstOrDefault(c => c.IpAddress == ipAddress);
        if (client == null)
        {
            client = new ClientInfo
            {
                IpAddress = ipAddress,
                LastUpdateTime = DateTime.Now,
                LastScreenshot = screenshot
            };
            Clients.Add(client);
            AddLog($"New client connected: {ipAddress}");
        }
        else
        {
            client.LastUpdateTime = DateTime.Now;
            client.LastScreenshot = screenshot;
        }
    }

    public void AddLog(string message)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        });
    }
}