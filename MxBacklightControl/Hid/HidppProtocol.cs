using System.Text;
using MxBacklightControl.Models;

namespace MxBacklightControl.Hid;

public sealed record BacklightConfig(
    bool Enabled,
    byte Options,
    byte SupportedOptions,
    byte Effect,
    byte Level);

public sealed record BacklightLevelInfo(
    int LevelCount,
    int CurrentLevel);

public sealed record BatteryInfo(
    int Percent,
    bool IsCharging);

public sealed class HidppKeyboardSession : IDisposable
{
    private const byte ReportIdShort = 0x10;
    private const byte ReportIdLong = 0x11;
    private const byte SwId = 0x0A;

    private const ushort FeatureRoot = 0x0000;
    private const ushort FeatureDeviceName = 0x0005;
    private const ushort FeatureBatteryStatus = 0x1000;
    private const ushort FeatureUnifiedBattery = 0x1004;
    private const ushort FeatureBacklight2 = 0x1982;

    private readonly HidDeviceStream _stream;
    private readonly object _ioLock = new();
    private bool _disposed;

    public byte DeviceIndex { get; }
    public ConnectionType ConnectionType { get; }
    public string DeviceName { get; private set; }
    public int ProtocolMajor { get; private set; }
    public int ProtocolMinor { get; private set; }
    public byte BacklightFeatureIndex { get; private set; }
    public byte BacklightFeatureVersion { get; private set; }
    public byte SupportedOptions { get; private set; }
    public byte Battery1000Index { get; private set; }
    public byte Battery1004Index { get; private set; }

    private HidppKeyboardSession(HidDeviceStream stream, byte deviceIndex, ConnectionType connectionType)
    {
        _stream = stream;
        DeviceIndex = deviceIndex;
        ConnectionType = connectionType;
        DeviceName = string.IsNullOrWhiteSpace(stream.DeviceInfo.ProductName)
            ? "Logitech MX Klavye"
            : stream.DeviceInfo.ProductName.Replace('_', ' ');
    }

    public static HidppKeyboardSession? Discover()
    {
        var devices = NativeHid.EnumerateLogitechHidppDevices();

        // Prioritize BLE HID++ (0xFF43 / 0x0202) and long-report USB/Bolt/Unifying (0xFF00 / output >= 20)
        var ordered = devices
            .OrderByDescending(d => d.UsagePage == 0xFF43 && d.Usage == 0x0202)
            .ThenByDescending(d => d.OutputReportByteLength >= 20)
            .ToList();

        foreach (var devInfo in ordered)
        {
            var connType = ClassifyConnection(devInfo);
            var candidateIndices = GetCandidateDeviceIndices(connType);

            HidDeviceStream? stream = HidDeviceStream.Open(devInfo);
            if (stream is null)
            {
                continue;
            }

            bool matched = false;
            try
            {
                foreach (byte devIdx in candidateIndices)
                {
                    var session = new HidppKeyboardSession(stream, devIdx, connType);
                    if (session.ProbeAndInitialize())
                    {
                        matched = true;
                        return session;
                    }
                }
            }
            catch
            {
                // Ignore probe failures on non-keyboard endpoints
            }
            finally
            {
                if (!matched)
                {
                    stream.Dispose();
                }
            }
        }

        return null;
    }

    private static ConnectionType ClassifyConnection(HidDeviceInfo info)
    {
        if (info.UsagePage == 0xFF43)
        {
            return ConnectionType.BluetoothLE;
        }

        return info.ProductId switch
        {
            0xC548 => ConnectionType.LogiBolt,
            0xC52B or 0xC52F or 0xC532 or 0xC534 => ConnectionType.Unifying,
            _ => ConnectionType.UsbDirect
        };
    }

    private static byte[] GetCandidateDeviceIndices(ConnectionType connType) => connType switch
    {
        ConnectionType.BluetoothLE => [0xFF],
        ConnectionType.LogiBolt or ConnectionType.Unifying => [0x01, 0x02, 0x03, 0x04, 0x05, 0x06],
        _ => [0xFF, 0x01]
    };

    private bool ProbeAndInitialize()
    {
        // 1. Ping IRoot (0x0000, fn 0x01)
        var ping = SendRequest(0x00, 0x01, [0x00, 0x00, 0xAA], timeoutMs: 800);
        if (ping is null || ping.Length < 7 || ping[2] == 0xFF)
        {
            return false;
        }

        ProtocolMajor = ping[4];
        ProtocolMinor = ping[5];

        // 2. Query Backlight v2 (0x1982)
        var blFeat = GetFeatureIndex(FeatureBacklight2);
        if (blFeat.Index == 0)
        {
            return false;
        }

        BacklightFeatureIndex = blFeat.Index;
        BacklightFeatureVersion = blFeat.Version;

        // 3. Query optional features: Device Name & Battery
        var nameFeat = GetFeatureIndex(FeatureDeviceName);
        if (nameFeat.Index != 0)
        {
            string? resolvedName = ReadDeviceName(nameFeat.Index);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                DeviceName = resolvedName;
            }
        }

        Battery1000Index = GetFeatureIndex(FeatureBatteryStatus).Index;
        if (Battery1000Index == 0)
        {
            Battery1004Index = GetFeatureIndex(FeatureUnifiedBattery).Index;
        }

        // 4. Read initial Backlight config to capture supported options mask
        var cfg = GetBacklightConfig();
        if (cfg is not null)
        {
            SupportedOptions = cfg.SupportedOptions;
        }

        return true;
    }

    private (byte Index, byte Version) GetFeatureIndex(ushort featureId)
    {
        byte hi = (byte)(featureId >> 8);
        byte lo = (byte)(featureId & 0xFF);
        var resp = SendRequest(0x00, 0x00, [hi, lo], timeoutMs: 800);
        if (resp is null || resp.Length < 7 || resp[2] == 0xFF)
        {
            return (0, 0);
        }

        return (resp[4], resp[6]);
    }

    private string? ReadDeviceName(byte featureIndex)
    {
        var lenResp = SendRequest(featureIndex, 0x00, [], timeoutMs: 800);
        if (lenResp is null || lenResp.Length < 5 || lenResp[2] == 0xFF)
        {
            return null;
        }

        int totalLen = lenResp[4];
        if (totalLen <= 0 || totalLen > 64)
        {
            return null;
        }

        var sb = new StringBuilder(totalLen);
        int offset = 0;
        while (offset < totalLen)
        {
            var chunkResp = SendRequest(featureIndex, 0x01, [(byte)offset], timeoutMs: 800);
            if (chunkResp is null || chunkResp.Length < 5 || chunkResp[2] == 0xFF)
            {
                break;
            }

            int take = Math.Min(totalLen - offset, chunkResp.Length - 4);
            string piece = Encoding.UTF8.GetString(chunkResp, 4, take).TrimEnd('\0');
            sb.Append(piece);
            offset += take;
            if (take == 0)
            {
                break;
            }
        }

        return sb.ToString().Trim();
    }

    public bool Ping()
    {
        var resp = SendRequest(0x00, 0x01, [0x00, 0x00, 0x5A], timeoutMs: 1000);
        return resp is not null && resp.Length >= 7 && resp[2] != 0xFF && resp[6] == 0x5A;
    }

    public BacklightConfig? GetBacklightConfig()
    {
        var resp = SendRequest(BacklightFeatureIndex, 0x00, [], timeoutMs: 1200);
        if (resp is null || resp.Length < 9 || resp[2] == 0xFF)
        {
            return null;
        }

        bool enabled = (resp[4] & 0x01) != 0;
        byte options = resp[5];
        byte supported = resp[6];
        byte effect = resp[7];
        byte level = resp[8];

        if (supported != 0)
        {
            SupportedOptions = supported;
        }

        return new BacklightConfig(enabled, options, supported, effect, level);
    }

    public bool SetBacklightConfig(bool enabled, byte options, byte? manualLevel = null, ushort? durationUnits = null)
    {
        byte enabledByte = enabled ? (byte)0x01 : (byte)0x00;
        byte[] payload;

        if (BacklightFeatureVersion >= 3)
        {
            // Feature 0x1982 v3 (MX Keys S / MX Mechanical):
            // [enabled, options(bits 3..4 = mode), effect, level, dho_hi, dho_lo, dhi_hi, dhi_lo, dpow_hi, dpow_lo]
            byte level = manualLevel ?? 5;
            ushort dur = durationUnits ?? 360; // 360 * 5s = 30 min
            payload =
            [
                enabledByte,
                options,
                0x00, // static effect
                level,
                (byte)(dur >> 8), (byte)(dur & 0xFF),
                (byte)(dur >> 8), (byte)(dur & 0xFF),
                (byte)(dur >> 8), (byte)(dur & 0xFF)
            ];
        }
        else
        {
            // Feature 0x1982 v0/v1/v2 (MX Keys, MX Keys Mini, Craft):
            // [enabled, options]
            payload = [enabledByte, options];
        }

        var resp = SendRequest(BacklightFeatureIndex, 0x01, payload, timeoutMs: 1200);
        return resp is not null && resp.Length >= 4 && resp[2] != 0xFF;
    }

    public BacklightLevelInfo? GetBacklightLevelInfo()
    {
        var resp = SendRequest(BacklightFeatureIndex, 0x02, [], timeoutMs: 1000);
        if (resp is null || resp.Length < 6 || resp[2] == 0xFF)
        {
            return null;
        }

        int count = resp[4];
        int current = resp[5];
        return new BacklightLevelInfo(count > 0 ? count : 8, current);
    }

    public BatteryInfo? GetBatteryInfo()
    {
        if (Battery1000Index != 0)
        {
            var resp = SendRequest(Battery1000Index, 0x00, [], timeoutMs: 1000);
            if (resp is not null && resp.Length >= 7 && resp[2] != 0xFF)
            {
                int percent = Math.Clamp((int)resp[4], 0, 100);
                // 0 = discharging, 1..4 = recharging/almost full/full
                bool charging = resp[6] is 1 or 2 or 3;
                return new BatteryInfo(percent, charging);
            }
        }

        if (Battery1004Index != 0)
        {
            var resp = SendRequest(Battery1004Index, 0x01, [], timeoutMs: 1000);
            if (resp is not null && resp.Length >= 7 && resp[2] != 0xFF)
            {
                int percent = Math.Clamp((int)resp[4], 0, 100);
                bool charging = resp[6] is 1 or 2 or 3;
                return new BatteryInfo(percent, charging);
            }
        }

        return null;
    }

    private byte[]? SendRequest(byte featureIndex, byte functionId, byte[] parameters, int timeoutMs)
    {
        lock (_ioLock)
        {
            if (_disposed)
            {
                return null;
            }

            byte[] packet = new byte[20];
            packet[0] = ReportIdLong;
            packet[1] = DeviceIndex;
            packet[2] = featureIndex;
            packet[3] = (byte)(((functionId & 0x0F) << 4) | (SwId & 0x0F));

            if (parameters.Length > 0)
            {
                int copyLen = Math.Min(parameters.Length, 16);
                Buffer.BlockCopy(parameters, 0, packet, 4, copyLen);
            }

            _stream.FlushInput();
            if (!_stream.WriteReport(packet, timeoutMs))
            {
                return null;
            }

            // Read up to 8 packets to skip any unrelated HID++ event notifications
            var deadline = Environment.TickCount64 + timeoutMs;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int remaining = (int)(deadline - Environment.TickCount64);
                if (remaining <= 0)
                {
                    break;
                }

                var resp = _stream.ReadReport(remaining);
                if (resp is null || resp.Length < 4)
                {
                    return null;
                }

                if (resp[0] != ReportIdLong && resp[0] != ReportIdShort)
                {
                    continue;
                }

                // Match device index (or BLE 0xFF)
                if (resp[1] != DeviceIndex && DeviceIndex != 0xFF)
                {
                    continue;
                }

                // Direct feature/function/swId match
                if (resp[2] == featureIndex && (resp[3] & 0x0F) == SwId)
                {
                    return resp;
                }

                // HID++ 2.0 error report: resp[2] == 0xFF, resp[3] == featureIndex, resp[4] == (fn<<4)|swId
                if (resp[2] == 0xFF && resp.Length >= 6 && resp[3] == featureIndex && (resp[4] & 0x0F) == SwId)
                {
                    return resp;
                }
            }

            return null;
        }
    }

    public void Dispose()
    {
        lock (_ioLock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _stream.Dispose();
        }
    }
}
