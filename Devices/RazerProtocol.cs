using BatteryMonitor.Hid;

namespace BatteryMonitor.Devices;

/// <summary>
/// Razer 2.4G 无线鼠标的控制协议：90 字节 Feature Report。
/// 请求 00 1F 00 00 02 07 80 …，回复首字节 0x02 表示成功，电量在回复字节 9（0-255）。
/// 只能走接收器的控制接口（mi_00），该接口常被系统鼠标驱动或 Synapse 独占。
/// </summary>
internal static class RazerProtocol
{
    public const int Vid = 0x1532;

    private const int ReportSize = 90;
    private const int BatteryOffset = 9;
    private const byte TransactionId = 0x1F;
    private const byte CommandClass = 0x07;
    private const byte CommandGetBattery = 0x80;

    public static bool IsSupported(List<HidInterface> group) =>
        group.Any(d => d.Vid == Vid) &&
        group.All(d => !d.Product.Contains("Dock", StringComparison.OrdinalIgnoreCase));

    public static HidInterface? FindControlInterface(List<HidInterface> group)
    {
        return group.FirstOrDefault(d => d.Path.Contains("mi_00", StringComparison.OrdinalIgnoreCase))
               ?? group.FirstOrDefault(d => d.FeatureLength >= ReportSize)
               ?? group.FirstOrDefault();
    }

    public static BatteryResult? ReadBattery(List<HidInterface> group)
    {
        var iface = FindControlInterface(group);
        if (iface is null)
            return BatteryResult.Fail("Razer 无线外设", "找不到接收器的控制接口");

        try
        {
            using var dev = new HidDevice(iface.Path, write: true);
            if (!dev.WriteAccess)
                return BatteryResult.Fail("Razer 无线外设", "控制接口被占用，无法发送查询命令");

            dev.SetFeatureReport(BuildRequest());
            var response = dev.GetFeatureReport(0x00, ReportSize);

            if (response.Length <= BatteryOffset)
                return BatteryResult.Fail("Razer 无线外设", $"回复长度不足（{response.Length} 字节）");

            if (response[0] != 0x02)
                return BatteryResult.Fail("Razer 无线外设", $"设备拒绝查询（状态字节 0x{response[0]:X2}）");

            int raw = response[BatteryOffset];
            if (raw == 0)
                return BatteryResult.Fail("Razer 无线外设", "设备没有上报电量（确认鼠标已开机并连上接收器）");

            return new BatteryResult
            {
                Ok = true,
                Level = BatteryReader.Scale(raw, 255),
                Source = "Razer 控制协议（0x07/0x80 查询电量）",
                Detail = $"回复字节 9 = 0x{raw:X2}（{raw}/255）",
            };
        }
        catch (HidException e)
        {
            string hint = e.ErrorCode == 5
                ? "拒绝访问（控制接口被鼠标驱动或 Razer Synapse 独占，关掉 Synapse 后重试）"
                : e.Message;
            return BatteryResult.Fail("Razer 无线外设", $"读取失败：{hint}");
        }
    }

    private static byte[] BuildRequest()
    {
        var buffer = new byte[ReportSize];
        buffer[1] = TransactionId;
        buffer[4] = 0x02;
        buffer[5] = CommandClass;
        buffer[6] = CommandGetBattery;
        return buffer;
    }
}
