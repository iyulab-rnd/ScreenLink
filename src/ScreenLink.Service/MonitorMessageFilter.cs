namespace ScreenLink.Service;

public class MonitorMessageFilter : IMessageFilter
{
    private const int WM_DISPLAYCHANGE = 0x007E;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == WM_DISPLAYCHANGE)
        {
            Console.WriteLine("Display configuration changed");
        }
        return false;
    }
}
