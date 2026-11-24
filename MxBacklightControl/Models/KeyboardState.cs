namespace MxBacklightControl.Models;

public enum KeyboardStatusKind
{
    Searching,
    Reconnecting,
    DisconnectedRescanning,
    SleepingOrDisconnected,
    NotFound,
    CommunicationError,
    ModeAlwaysOn,
    ModeAlwaysOff,
    ModeAuto
}

public sealed class KeyboardState
{
    public bool IsConnected { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Unknown;

    public int ProtocolMajor { get; set; }
    public int ProtocolMinor { get; set; }
    public byte BacklightFeatureIndex { get; set; }
    public byte BacklightFeatureVersion { get; set; }
    public byte SupportedOptions { get; set; }
    public bool HardwareBacklightEnabled { get; set; }
    public byte HardwareOptions { get; set; }
    public int MaxBrightnessLevels { get; set; } = 7;
    public int CurrentBrightnessLevel { get; set; }

    public bool HasBattery { get; set; }
    public int BatteryPercent { get; set; }
    public bool IsCharging { get; set; }

    public KeyboardStatusKind StatusKind { get; set; } = KeyboardStatusKind.Searching;
    public string? ErrorDetail { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
