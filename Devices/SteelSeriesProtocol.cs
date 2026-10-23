using BatteryMonitor.Hid;

namespace BatteryMonitor.Devices;

/// <summary>
/// SteelSeries Arctis 7 / Arctis Pro（0x1038）2.4G 耳机。
/// 31 字节报文：写 06 18，回复第 2 字节即电量百分比。
/// </summary>
internal static class SteelSeriesProtocol
{
    public const int Vid = 0x1038;

    private static readonly int[] Pids = { 0x1260, 0x12AD, 0x1252, 0x1280 };

    private const int PacketSize = 31;
    private const int ResponseSize = 8;

    public static bool IsSupported(HidInterface iface) =>
        iface.Vid == Vid && Pids.Contains(iface.Pid);

    public static HidInterface? FindInterface(List<HidInterface> group) =>
        group.FirstOrDefault(d => IsSupported(d) && d.OutputLength == PacketSize)
        ?? group.FirstOrDefault(d => IsSupported(d) && d.OutputLength > 0)
        ?? group.FirstOrDefault(IsSupported);

    public static BatteryResult? ReadBattery(List<HidInterface> group)
    {
        var iface = FindInterface(group);
        if (iface is null) return null;

        if (iface.OutputLength == 0)
            return BatteryResult.Fail("SteelSeries 无线耳机", "接口没有输出报告，无法发送查询命令");

        try
        {
            using var dev = new HidDevice(iface.Path, write: true);
            if (!dev.WriteAccess)
                return BatteryResult.Fail("SteelSeries 无线耳机", "没有写权限（可能被 SteelSeries GG 占用）");

            for (int attempt = 0; attempt < 2; attempt++)
            {
                dev.Drain();

                var request = new byte[Math.Max(PacketSize, iface.OutputLength)];
                request[0] = 0x06;
                request[1] = 0x18;
                dev.WriteOutput(request);

                long deadline = Environment.TickCount64 + 700;
                while (Environment.TickCount64 < deadline)
                {
                    var report = dev.ReadInput(200, Math.Max(ResponseSize, iface.InputLength));
                    if (report is null) continue;

                    if (!TryParse(report, out int level, out int raw)) continue;

                    // 耳机休眠时接收器照样应答，但电量字节是 0，不能当成 0% 显示
                    if (raw == 0)
                        return BatteryResult.Fail("SteelSeries 无线耳机",
                            "接收器有应答但电量为 0（耳机多半已休眠，动一下耳机后重试）");

                    return new BatteryResult
                    {
                        Ok = true,
                        Level = level,
                        Source = "SteelSeries 私有协议（06 18 查询）",
                        Detail = $"回复第 2 字节 = {raw}（直接是百分比）",
                    };
                }
            }

            return BatteryResult.Fail("SteelSeries 无线耳机",
                "耳机没有回复电量（确认耳机已开机并连上接收器）");
        }
        catch (HidException e)
        {
            return BatteryResult.Fail("SteelSeries 无线耳机", $"收发失败：{e.Message}");
        }
    }

    private static bool TryParse(byte[] report, out int level, out int raw)
    {
        level = 0;
        raw = 0;

        // 回复的前两字节是命令回显（06 18），第 3 字节才是电量
        if (report.Length < 3) return false;

        int value = report[2];
        if (value > 100) return false;

        raw = value;
        level = value;
        return true;
    }
}
