using System.IO;
using System.Windows;

namespace ScreenLink.Server
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // HTTP/2 without TLS 허용
            //Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:5001");
            Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Protocols", "Http2");

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}