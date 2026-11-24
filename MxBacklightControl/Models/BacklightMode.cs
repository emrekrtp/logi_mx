namespace MxBacklightControl.Models;

public enum BacklightMode
{
    Auto = 0,
    AlwaysOn = 1,
    AlwaysOff = 2
}

public enum ConnectionType
{
    Unknown = 0,
    BluetoothLE = 1,
    LogiBolt = 2,
    Unifying = 3,
    UsbDirect = 4
}
