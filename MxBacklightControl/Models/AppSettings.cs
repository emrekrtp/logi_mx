namespace MxBacklightControl.Models;

public sealed class AppSettings
{
    public BacklightMode Mode { get; set; } = BacklightMode.Auto;
    public int KeepAliveIntervalSeconds { get; set; } = 4;
    public int ManualBrightnessLevel { get; set; } = 5;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool LaunchAtStartup { get; set; } = false;
    public bool EnableGlobalHotkey { get; set; } = true;
}
