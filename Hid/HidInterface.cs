using BatteryMonitor.Native;

namespace BatteryMonitor.Hid;

internal sealed class HidInterface
{
    public string Path { get; init; } = "";
    public int Vid { get; init; }
    public int Pid { get; init; }
    public int Version { get; init; }
    public int UsagePage { get; init; }
    public int Usage { get; init; }
    public int InputLength { get; init; }
    public int OutputLength { get; init; }
    public int FeatureLength { get; init; }
    public string Product { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Serial { get; init; } = "";
    public string Error { get; init; } = "";
    public string Group { get; init; } = "";

    public string VidPid => $"{Vid:X4}:{Pid:X4}";

    public bool IsReserved => HidEnumerator.SystemReservedUsages.Contains((UsagePage, Usage));

    public bool IsVendorPage => UsagePage >= 0xFF00;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Product)) return Product;
            if (!string.IsNullOrWhiteSpace(Manufacturer)) return $"{Manufacturer} HID 设备";
            return "HID 设备（无产品名字符串）";
        }
    }

    public override string ToString() => $"{DisplayName} [{VidPid} {UsagePage:X4}:{Usage:X4}]";

    internal static HidInterface Probe(string path, int vidHint, int pidHint)
    {
        int vid = vidHint, pid = pidHint, version = 0;
        int usagePage = 0, usage = 0, inputLen = 0, outputLen = 0, featureLen = 0;
        string product = "", manufacturer = "", serial = "", error = "";

        IntPtr handle = Win32.CreateFileW(path, 0, Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
            IntPtr.Zero, Win32.OPEN_EXISTING, 0, IntPtr.Zero);

        if (handle == Win32.INVALID_HANDLE_VALUE || handle == IntPtr.Zero)
        {
            error = $"无法打开（{System.Runtime.InteropServices.Marshal.GetLastWin32Error()}）";
        }
        else
        {
            try
            {
                var attrs = new Win32.HIDD_ATTRIBUTES { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.HIDD_ATTRIBUTES>() };
                if (Win32.HidD_GetAttributes(handle, ref attrs))
                {
                    vid = attrs.VendorID;
                    pid = attrs.ProductID;
                    version = attrs.VersionNumber;
                }

                product = ReadWideString(Win32.HidD_GetProductString, handle);
                manufacturer = ReadWideString(Win32.HidD_GetManufacturerString, handle);
                serial = ReadWideString(Win32.HidD_GetSerialNumberString, handle);

                if (Win32.HidD_GetPreparsedData(handle, out IntPtr preparsed) && preparsed != IntPtr.Zero)
                {
                    try
                    {
                        var caps = new Win32.HIDP_CAPS();
                        if (Win32.HidP_GetCaps(preparsed, ref caps) == Win32.HIDP_STATUS_SUCCESS)
                        {
                            usagePage = caps.UsagePage;
                            usage = caps.Usage;
                            inputLen = caps.InputReportByteLength;
                            outputLen = caps.OutputReportByteLength;
                            featureLen = caps.FeatureReportByteLength;
                        }
                    }
                    finally
                    {
                        Win32.HidD_FreePreparsedData(preparsed);
                    }
                }
            }
            finally
            {
                Win32.CloseHandle(handle);
            }
        }

        return new HidInterface
        {
            Path = path,
            Vid = vid,
            Pid = pid,
            Version = version,
            UsagePage = usagePage,
            Usage = usage,
            InputLength = inputLen,
            OutputLength = outputLen,
            FeatureLength = featureLen,
            Product = product,
            Manufacturer = manufacturer,
            Serial = serial,
            Error = error,
            Group = PhysicalGroupKey(path),
        };
    }

    private static string ReadWideString(Func<IntPtr, byte[], uint, bool> func, IntPtr handle)
    {
        var buffer = new byte[512];
        try
        {
            if (!func(handle, buffer, (uint)buffer.Length)) return "";
        }
        catch
        {
            return "";
        }

        string s = System.Text.Encoding.Unicode.GetString(buffer);
        int nul = s.IndexOf('\0');
        if (nul >= 0) s = s[..nul];
        return s.Trim();
    }

    internal static string PhysicalGroupKey(string path)
    {
        string[] parts = path.Split('#');
        if (parts.Length < 2) return path;

        var keep = new List<string>();
        foreach (string p in parts[1].Split('&'))
        {
            if (p.StartsWith("col", StringComparison.OrdinalIgnoreCase)) continue;
            if (p.StartsWith("mi_", StringComparison.OrdinalIgnoreCase)) continue;
            keep.Add(p);
        }
        return keep.Count > 0 ? string.Join("&", keep) : parts[1];
    }
}
