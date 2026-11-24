namespace MxBacklightControl.Models;

public sealed class KeyboardState
{
    public bool IsConnected { get; set; }
    public string DeviceName { get; set; } = "Klavye aranıyor...";
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Unknown;
    public string ConnectionLabel => ConnectionType switch
    {
        ConnectionType.BluetoothLE => "Bluetooth LE",
        ConnectionType.LogiBolt => "Logi Bolt",
        ConnectionType.Unifying => "Unifying",
        ConnectionType.UsbDirect => "USB",
        _ => "Bağlı Değil"
    };

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

    public string StatusMessage { get; set; } = "Logitech MX klavye bekleniyor...";
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
