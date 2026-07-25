namespace LocalPlay.Models;

public sealed class AppSettings
{
    public string ReceiverName { get; set; } = "LocalPlay";
    public bool RequirePin { get; set; } = true;
    public bool Fullscreen { get; set; }
    public string Quality { get; set; } = "1080p · 30 FPS";
    public string NetworkAdapterId { get; set; } = string.Empty;
    public int PortStart { get; set; } = 7000;
}
