using BatteryMonitor.Hid;

namespace BatteryMonitor.Devices;

internal sealed class BatteryResult
{
    public bool Ok { get; init; }
    public int? Level { get; init; }
    public string Status { get; init; } = "unknown";
    public string Source { get; init; } = "";
    public string Detail { get; init; } = "";

    public string StatusText => Status switch
    {
        "discharging" => "放电中",
        "charging" => "充电中",
        "full" => "已充满",
        _ => "未知",
    };

    public static BatteryResult Fail(string source, string detail) =>
        new() { Ok = false, Source = source, Detail = detail };
}

internal static class BatteryReader
{
    // 标准 HID 电量相关 usage（UsagePage, Usage, 说明）
    private static readonly (int Page, int Usage, string Label)[] BatteryUsages =
    {
        (0x0006, 0x0020, "Battery Strength（通用设备控制页）"),
        (0x000D, 0x003B, "Battery Strength（数字化仪页）"),
        (0x0085, 0x0001, "Absolute State of Charge"),
        (0x0085, 0x0066, "Relative State of Charge"),
        (0x0085, 0x0065, "Battery Level"),
        (0x0006, 0x0029, "RF Signal Strength"),
    };

    public static int? Scale(int value, int logicalMax = 100)
    {
        if (logicalMax > 0 && logicalMax != 100)
        {
            if (logicalMax == 0xFF && value > 100)
                return Math.Clamp((int)Math.Round(value * 100.0 / 255), 0, 100);
            return Math.Clamp((int)Math.Round(value * 100.0 / logicalMax), 0, 100);
        }
        if (value > 100) return Math.Clamp((int)Math.Round(value * 100.0 / 255), 0, 100);
        return Math.Max(0, value);
    }

    /// <summary>接口是否声明了标准电量相关 usage（Feature Report）。</summary>
    public static bool HasBatteryUsage(HidInterface iface)
    {
        if (iface.FeatureLength == 0) return false;

        try
        {
            using var dev = new HidDevice(iface.Path);
            foreach (var cap in dev.ValueCaps(Native.Win32.HidP_Feature))
            {
                ushort usage = cap.IsRange != 0 ? cap.Union.Range.UsageMin : cap.Union.NotRange.Usage;
                if (BatteryUsages.Any(b => b.Page == cap.UsagePage && b.Usage == usage)) return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static BatteryResult? TryStandardUsage(HidInterface iface)
    {
        if (iface.FeatureLength == 0) return null;

        try
        {
            using var dev = new HidDevice(iface.Path);
            foreach (var cap in dev.ValueCaps(Native.Win32.HidP_Feature))
            {
                ushort usage = cap.IsRange != 0 ? cap.Union.Range.UsageMin : cap.Union.NotRange.Usage;
                var match = BatteryUsages.FirstOrDefault(b => b.Page == cap.UsagePage && b.Usage == usage);
                if (match.Label is null) continue;

                byte[] report;
                try
                {
                    report = dev.GetFeatureReport(cap.ReportID);
                }
                catch (HidException e)
                {
                    return BatteryResult.Fail("标准 HID 电量字段",
                        $"接口声明了 {match.Label}，但读取 Feature Report 0x{cap.ReportID:X2} 失败：{e.Message}");
                }

                uint? value = dev.UsageValue(Native.Win32.HidP_Feature, report, cap);
                if (value is null) continue;

                return new BatteryResult
                {
                    Ok = true,
                    Level = Scale((int)value.Value, cap.LogicalMax),
                    Source = $"标准 HID 电量字段（{match.Label}）",
                    Detail = $"UsagePage=0x{cap.UsagePage:X4} Usage=0x{usage:X4} " +
                             $"ReportID=0x{cap.ReportID:X2} 原始值={value} 上限={cap.LogicalMax}",
                };
            }
        }
        catch (HidException e)
        {
            return BatteryResult.Fail("标准 HID 电量字段", e.Message);
        }
        return null;
    }
}
