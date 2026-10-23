using System.Runtime.InteropServices;
using BatteryMonitor.Native;

namespace BatteryMonitor.Hid;

internal static class HidEnumerator
{
    public static readonly HashSet<(int, int)> SystemReservedUsages = new()
    {
        (0x0001, 0x0001), // Pointer
        (0x0001, 0x0002), // Mouse
        (0x0001, 0x0006), // Keyboard
        (0x0001, 0x0007), // Keypad
        (0x000D, 0x0001), // Pen
        (0x000D, 0x0002), // Pen
        (0x000D, 0x0004), // Touchscreen
        (0x000D, 0x0005), // Precision Touchpad
    };

    public static List<HidInterface> Enumerate()
    {
        var result = new List<HidInterface>();
        Guid guid = Win32.HidInterfaceClassGuid;

        IntPtr deviceInfoSet = Win32.SetupDiGetClassDevsW(guid, null, IntPtr.Zero,
            Win32.DIGCF_PRESENT | Win32.DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == Win32.INVALID_HANDLE_VALUE || deviceInfoSet == IntPtr.Zero)
            throw new IOException($"SetupDiGetClassDevsW 失败，err={Marshal.GetLastWin32Error()}");

        try
        {
            for (uint index = 0; ; index++)
            {
                var iface = new Win32.SP_DEVICE_INTERFACE_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<Win32.SP_DEVICE_INTERFACE_DATA>()
                };

                if (!Win32.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, guid, index, ref iface))
                    break;

                string? path = GetInterfacePath(deviceInfoSet, ref iface);
                if (string.IsNullOrEmpty(path)) continue;

                result.Add(HidInterface.Probe(path, ParseId(path, "vid_"), ParseId(path, "pid_")));
            }
        }
        finally
        {
            Win32.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return result;
    }

    private static string? GetInterfacePath(IntPtr deviceInfoSet, ref Win32.SP_DEVICE_INTERFACE_DATA iface)
    {
        if (!Win32.SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref iface, IntPtr.Zero, 0,
                out uint requiredSize, IntPtr.Zero) && requiredSize == 0)
            return null;

        IntPtr buffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            // cbSize: 32 位为 6，64 位为 8
            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);

            if (!Win32.SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref iface, buffer,
                    requiredSize, out _, IntPtr.Zero))
                return null;

            return Marshal.PtrToStringUni(buffer + 4);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int ParseId(string path, string key)
    {
        string lower = path.ToLowerInvariant();
        int at = lower.IndexOf(key, StringComparison.Ordinal);
        if (at < 0) return 0;
        at += key.Length;
        if (at + 4 > lower.Length) return 0;
        return int.TryParse(lower.AsSpan(at, 4), System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : 0;
    }
}
