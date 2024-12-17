using MessagePack;

namespace ScreenLink.Common;

[MessagePackObject]
public class ScreenshotMessage
{
    [Key(0)]
    public string Timestamp { get; set; }

    [Key(1)]
    public byte[] ImageData { get; set; }
}