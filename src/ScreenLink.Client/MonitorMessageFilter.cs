using Forms = System.Windows.Forms;

public class MonitorMessageFilter : Forms.IMessageFilter
{
    private const int WM_DISPLAYCHANGE = 0x007E;

    public bool PreFilterMessage(ref Forms.Message m)
    {
        if (m.Msg == WM_DISPLAYCHANGE)
        {
            Console.WriteLine("Display configuration changed");
        }
        return false;
    }
}
