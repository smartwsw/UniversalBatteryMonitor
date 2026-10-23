using System.Runtime.InteropServices;
using BatteryMonitor.Native;

namespace BatteryMonitor.Hid;

internal sealed class HidException : Exception
{
    public HidException(string message, int errorCode = 0) : base(message)
    {
        ErrorCode = errorCode;
    }

    public int ErrorCode { get; }
}

/// <summary>
/// 一个已打开的 HID 接口句柄。
/// 收 Input Report 时用独立的 OVERLAPPED 读句柄 + 事件超时 + CancelIoEx：
/// 同一个句柄上并发"阻塞读 + 写"在 HID 驱动层会让进程直接退出。
/// </summary>
internal sealed class HidDevice : IDisposable
{
    private readonly string _path;
    private IntPtr _handle;
    private IntPtr _preparsed;
    private IntPtr _readHandle;
    private IntPtr _readEvent;
    private Win32.HIDP_CAPS? _caps;
    private bool _disposed;

    public HidDevice(string path, bool write = false)
    {
        _path = path;
        uint access = write ? Win32.GENERIC_READ | Win32.GENERIC_WRITE : 0;

        _handle = Win32.CreateFileW(path, access, Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
            IntPtr.Zero, Win32.OPEN_EXISTING, 0, IntPtr.Zero);

        if ((_handle == Win32.INVALID_HANDLE_VALUE || _handle == IntPtr.Zero) && write)
        {
            _handle = Win32.CreateFileW(path, 0, Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
                IntPtr.Zero, Win32.OPEN_EXISTING, 0, IntPtr.Zero);
            WriteAccess = false;
        }
        else
        {
            WriteAccess = write;
        }

        if (_handle == Win32.INVALID_HANDLE_VALUE || _handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new HidException($"CreateFileW 失败 (err={err})", err);
        }

        if (!Win32.HidD_GetPreparsedData(_handle, out _preparsed))
            _preparsed = IntPtr.Zero;
    }

    public bool WriteAccess { get; }

    public Win32.HIDP_CAPS Caps
    {
        get
        {
            if (_caps is null)
            {
                if (_preparsed == IntPtr.Zero)
                    throw new HidException("没有 preparsed data，无法读取能力表");

                var caps = new Win32.HIDP_CAPS();
                if (Win32.HidP_GetCaps(_preparsed, ref caps) != Win32.HIDP_STATUS_SUCCESS)
                    throw new HidException("HidP_GetCaps 失败");

                _caps = caps;
            }
            return _caps.Value;
        }
    }

    public List<Win32.HIDP_VALUE_CAPS> ValueCaps(int reportType)
    {
        var list = new List<Win32.HIDP_VALUE_CAPS>();
        if (_preparsed == IntPtr.Zero) return list;

        ushort length = 0;
        Win32.HidP_GetValueCaps(reportType, null, ref length, _preparsed);
        if (length == 0) return list;

        var arr = new Win32.HIDP_VALUE_CAPS[length];
        if (Win32.HidP_GetValueCaps(reportType, arr, ref length, _preparsed) != Win32.HIDP_STATUS_SUCCESS)
            return list;

        for (int i = 0; i < length; i++) list.Add(arr[i]);
        return list;
    }

    public byte[] GetFeatureReport(int reportId, int? length = null)
    {
        int len = length ?? (Caps.FeatureReportByteLength > 0 ? Caps.FeatureReportByteLength : 64);
        var buffer = new byte[len];
        buffer[0] = (byte)(reportId & 0xFF);

        if (!Win32.HidD_GetFeature(_handle, buffer, (uint)len))
            throw new HidException($"读 Feature Report 0x{reportId:X2} 失败 (err={Marshal.GetLastWin32Error()})",
                Marshal.GetLastWin32Error());

        return buffer;
    }

    public void SetFeatureReport(byte[] data)
    {
        if (!WriteAccess) throw new HidException("当前句柄没有写权限，无法发送 Feature Report");
        if (!Win32.HidD_SetFeature(_handle, data, (uint)data.Length))
            throw new HidException($"写 Feature Report 失败 (err={Marshal.GetLastWin32Error()})",
                Marshal.GetLastWin32Error());
    }

    public void WriteOutput(byte[] data)
    {
        if (!WriteAccess) throw new HidException("当前句柄没有写权限，无法发送 Output Report");

        if (Win32.HidD_SetOutputReport(_handle, data, (uint)data.Length)) return;

        int err = Marshal.GetLastWin32Error();
        if (!Win32.WriteFile(_handle, data, (uint)data.Length, out _, IntPtr.Zero))
            throw new HidException($"WriteFile 失败 (err={Marshal.GetLastWin32Error()})", err);
    }

    public byte[]? ReadInput(int timeoutMs = 200, int? length = null, Func<bool>? stopFlag = null)
    {
        if (Caps.InputReportByteLength == 0) return null;
        if (!OpenReader()) return null;

        int len = length ?? Caps.InputReportByteLength;
        var buffer = new byte[len];
        IntPtr ptr = Marshal.AllocHGlobal(len);

        try
        {
            var ov = new Win32.OVERLAPPED { hEvent = _readEvent };
            Win32.ResetEvent(_readEvent);

            bool ok = Win32.ReadFile(_readHandle, ptr, (uint)len, out uint read, ref ov);
            if (ok)
            {
                Marshal.Copy(ptr, buffer, 0, (int)Math.Min(read, (uint)len));
                return buffer[..(int)read];
            }

            if (Marshal.GetLastWin32Error() != Win32.ERROR_IO_PENDING) return null;

            long deadline = Environment.TickCount64 + Math.Max(timeoutMs, 1);

            while (true)
            {
                if (stopFlag is not null && stopFlag())
                {
                    CancelRead(ref ov);
                    return null;
                }

                long remain = deadline - Environment.TickCount64;
                if (remain <= 0)
                {
                    CancelRead(ref ov);
                    return null;
                }

                uint wait = (uint)Math.Max(1, Math.Min(remain, 200));
                uint r = Win32.WaitForSingleObject(_readEvent, wait);

                if (r == Win32.WAIT_OBJECT_0)
                {
                    if (!Win32.GetOverlappedResult(_readHandle, ref ov, out uint done, false)) return null;
                    Marshal.Copy(ptr, buffer, 0, (int)Math.Min(done, (uint)len));
                    return buffer[..(int)done];
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private void CancelRead(ref Win32.OVERLAPPED ov)
    {
        Win32.CancelIoEx(_readHandle, ref ov);
        Win32.WaitForSingleObject(_readEvent, 200);
        Win32.GetOverlappedResult(_readHandle, ref ov, out _, false);
    }

    private bool OpenReader()
    {
        if (_readHandle != IntPtr.Zero) return true;

        IntPtr h = Win32.CreateFileW(_path, Win32.GENERIC_READ,
            Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE, IntPtr.Zero,
            Win32.OPEN_EXISTING, Win32.FILE_FLAG_OVERLAPPED, IntPtr.Zero);

        if (h == Win32.INVALID_HANDLE_VALUE || h == IntPtr.Zero) return false;

        IntPtr evt = Win32.CreateEventW(IntPtr.Zero, true, false, null);
        if (evt == IntPtr.Zero)
        {
            Win32.CloseHandle(h);
            return false;
        }

        _readHandle = h;
        _readEvent = evt;
        return true;
    }

    /// <summary>丢弃驱动里堆积的旧报文（发请求前调用）。</summary>
    public void Drain()
    {
        if (Caps.InputReportByteLength == 0) return;
        for (int i = 0; i < 64; i++)
        {
            if (ReadInput(8) is null) return;
        }
    }

    public uint? UsageValue(int reportType, byte[] report, Win32.HIDP_VALUE_CAPS caps)
    {
        if (_preparsed == IntPtr.Zero) return null;

        ushort usage = caps.IsRange != 0 ? caps.Union.Range.UsageMin : caps.Union.NotRange.Usage;

        int status = Win32.HidP_GetUsageValue(reportType, caps.UsagePage, caps.LinkCollection,
            usage, out uint value, _preparsed, report, (uint)report.Length);

        return status == Win32.HIDP_STATUS_SUCCESS ? value : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_readHandle != IntPtr.Zero)
        {
            Win32.CancelIo(_readHandle);
            Win32.CloseHandle(_readHandle);
            _readHandle = IntPtr.Zero;
        }

        if (_readEvent != IntPtr.Zero)
        {
            Win32.CloseHandle(_readEvent);
            _readEvent = IntPtr.Zero;
        }

        if (_preparsed != IntPtr.Zero)
        {
            Win32.HidD_FreePreparsedData(_preparsed);
            _preparsed = IntPtr.Zero;
        }

        if (_handle != IntPtr.Zero && _handle != Win32.INVALID_HANDLE_VALUE)
        {
            Win32.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
