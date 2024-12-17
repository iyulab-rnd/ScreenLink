using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenLink.Server.Models;

public partial class ClientInfo : ObservableObject
{
    [ObservableProperty]
    private string ipAddress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUpdateTimeString))]
    private DateTime lastUpdateTime;

    [ObservableProperty]
    private byte[] lastScreenshot;

    public string LastUpdateTimeString => LastUpdateTime.ToString("yyyy-MM-dd HH:mm:ss");
}