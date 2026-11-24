using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MxBacklightControl.Hid;

public sealed class HidDeviceStream : IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly int _inputReportLength;
    private readonly int _outputReportLength;
    private bool _disposed;

    public HidDeviceInfo DeviceInfo { get; }

    private HidDeviceStream(SafeFileHandle handle, HidDeviceInfo info)
    {
        _handle = handle;
        DeviceInfo = info;
        _inputReportLength = Math.Max((int)info.InputReportByteLength, 20);
        _outputReportLength = Math.Max((int)info.OutputReportByteLength, 20);
    }

    public static HidDeviceStream? Open(HidDeviceInfo info)
    {
        var handle = NativeHid.CreateFile(
            info.DevicePath,
            NativeHid.GENERIC_READ | NativeHid.GENERIC_WRITE,
            NativeHid.FILE_SHARE_READ | NativeHid.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeHid.OPEN_EXISTING,
            NativeHid.FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        NativeHid.HidD_FlushQueue(handle);
        return new HidDeviceStream(handle, info);
    }

    public void FlushInput()
    {
        if (!_disposed && !_handle.IsInvalid)
        {
            NativeHid.HidD_FlushQueue(_handle);
        }
    }

    public bool WriteReport(byte[] data, int timeoutMs = 1000)
    {
        if (_disposed || _handle.IsInvalid)
        {
            return false;
        }

        byte[] buffer = new byte[_outputReportLength];
        int copyLen = Math.Min(data.Length, buffer.Length);
        Buffer.BlockCopy(data, 0, buffer, 0, copyLen);

        using var evt = NativeHid.CreateEvent(IntPtr.Zero, true, false, null);
        if (evt.IsInvalid)
        {
            return false;
        }

        var overlapped = new NativeHid.NativeOverlappedEx
        {
            EventHandle = evt.DangerousGetHandle()
        };

        bool ok = NativeHid.WriteFile(_handle, buffer, (uint)buffer.Length, out _, ref overlapped);
        if (ok)
        {
            return true;
        }

        int err = Marshal.GetLastWin32Error();
        if (err != NativeHid.ERROR_IO_PENDING)
        {
            return false;
        }

        uint wait = NativeHid.WaitForSingleObject(evt, (uint)timeoutMs);
        if (wait == NativeHid.WAIT_OBJECT_0)
        {
            return NativeHid.GetOverlappedResult(_handle, ref overlapped, out _, false);
        }

        NativeHid.CancelIoEx(_handle, IntPtr.Zero);
        return false;
    }

    public byte[]? ReadReport(int timeoutMs = 1000)
    {
        if (_disposed || _handle.IsInvalid)
        {
            return null;
        }

        byte[] buffer = new byte[_inputReportLength];

        using var evt = NativeHid.CreateEvent(IntPtr.Zero, true, false, null);
        if (evt.IsInvalid)
        {
            return null;
        }

        var overlapped = new NativeHid.NativeOverlappedEx
        {
            EventHandle = evt.DangerousGetHandle()
        };

        bool ok = NativeHid.ReadFile(_handle, buffer, (uint)buffer.Length, out uint bytesRead, ref overlapped);
        if (ok)
        {
            return bytesRead > 0 ? buffer[..(int)bytesRead] : null;
        }

        int err = Marshal.GetLastWin32Error();
        if (err != NativeHid.ERROR_IO_PENDING)
        {
            return null;
        }

        uint wait = NativeHid.WaitForSingleObject(evt, (uint)timeoutMs);
        if (wait == NativeHid.WAIT_OBJECT_0)
        {
            if (NativeHid.GetOverlappedResult(_handle, ref overlapped, out bytesRead, false) && bytesRead > 0)
            {
                return buffer[..(int)bytesRead];
            }
            return null;
        }

        NativeHid.CancelIoEx(_handle, IntPtr.Zero);
        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _handle.Dispose();
    }
}
