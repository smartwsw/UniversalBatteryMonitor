using BatteryMonitor.App;
using BatteryMonitor.Hid;

namespace BatteryMonitor.Devices;

/// <summary>
/// Razer 2.4G 无线外设的控制协议：91 字节 Feature Report（首字节是 Report ID）。
/// 报文体 = buf[1..]，体中 [1]=事务号、[5]=参数长度、[6]=命令类、[7]=命令号，
/// 体中 [2..87] 做异或存到 [88]。回复体 [0]=状态（0x02 成功）、args = 体中 [8..88]。
/// 只有接收器的控制接口（mi_00，usage 0x0001:0x0002）支持，且必须以管理员权限打开。
/// </summary>
internal static class RazerProtocol
{
    public const int Vid = 0x1532;

    private const int ReportSize = 91;
    private const int BodyOffset = 1;
    private const byte CommandClass = 0x07;
    private const byte CommandGetBattery = 0x80;
    private const byte CommandGetCharging = 0x84;
    private const int ArgCount = 80;
    private const int ArgOffset = BodyOffset + 8;

    private static readonly byte[] TransactionIds = { 0x1F, 0xFF, 0x3F };

    private const byte StatusBusy = 0x01;
    private const byte StatusOk = 0x02;
    private const byte StatusUnsupported = 0x05;
    private const byte StatusTimeout = 0x04;

    public static bool IsSupported(List<HidInterface> group) =>
        group.Any(d => d.Vid == Vid) &&
        group.All(d => !d.Product.Contains("Dock", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 普通权限下接口打不开，能力表全是 0，所以这里只按 VID + mi_00 兜底，
    /// 让界面能给出"需要管理员权限"的提示，而不是"没有可用接口"。
    /// </summary>
    public static HidInterface? FindControlInterface(List<HidInterface> group) =>
        group.FirstOrDefault(IsControlInterface)
        ?? group.FirstOrDefault(d => d.Vid == Vid && d.Path.Contains("mi_00", StringComparison.OrdinalIgnoreCase));

    private static bool IsControlInterface(HidInterface d) =>
        d.Vid == Vid && d.UsagePage == 0x0001 && d.Usage == 0x0002 && d.FeatureLength >= ReportSize;

    public static BatteryResult? ReadBattery(List<HidInterface> group)
    {
        var candidates = group.Where(d => d.Vid == Vid && (IsControlInterface(d)
            || d.Path.Contains("mi_00", StringComparison.OrdinalIgnoreCase))).ToList();
        if (candidates.Count == 0)
            return BatteryResult.Fail("Razer 无线外设", "找不到控制接口（0x0001:0x0002，91 字节 Feature Report）");

        foreach (var iface in candidates)
        {
            var result = TryRead(iface);
            if (result is not null) return result;
        }

        return BatteryResult.Fail("Razer 无线外设", Elevation.IsAdmin()
            ? "控制接口拒绝访问（err=5），确认没有别的软件独占接收器"
            : "控制接口只允许管理员进程打开：右键托盘图标 → 以管理员身份重启");
    }

    private static BatteryResult? TryRead(HidInterface iface)
    {
        HidDevice dev;
        try
        {
            // 这个接口只允许 access=0 + 共享读写；GENERIC_READ|WRITE 一定 err=5。
            dev = new HidDevice(iface.Path, write: false);
        }
        catch (HidException)
        {
            return null;
        }

        using (dev)
        {
            var battery = Query(dev, CommandGetBattery);
            if (battery is null) return null;

            int raw = battery.Args[1];
            if (raw == 0 && battery.Status == StatusOk)
                return BatteryResult.Fail("Razer 无线外设", "设备没有上报电量（确认鼠标已开机并连上接收器）");

            var charging = Query(dev, CommandGetCharging);
            int level = BatteryReader.Scale(raw, 255) ?? 0;
            string status = charging is { Status: StatusOk } c
                ? (c.Args[1] == 1 ? (level >= 100 ? "full" : "charging") : "discharging")
                : "unknown";

            return new BatteryResult
            {
                Ok = true,
                Level = level,
                Status = status,
                Source = "Razer 控制协议",
                Detail = $"电量字节 0x{raw:X2}（{raw}/255），状态 0x{battery.Status:X2}",
            };
        }
    }

    private sealed record Reply(byte Status, byte[] Args);

    private static Reply? Query(HidDevice dev, byte command)
    {
        foreach (byte tx in TransactionIds)
        {
            try
            {
                dev.SetFeatureReport(BuildRequest(tx, command));
            }
            catch (HidException)
            {
                return null;
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                Thread.Sleep(attempt == 0 ? 5 : 20);

                byte[] reply;
                try
                {
                    reply = dev.GetFeatureReport(0x00, ReportSize);
                }
                catch (HidException)
                {
                    return null;
                }

                if (reply.Length < ReportSize || reply[BodyOffset + 6] != CommandClass || reply[BodyOffset + 7] != command)
                    continue;

                byte status = reply[BodyOffset];
                if (status is 0x00 or StatusBusy) continue;

                var args = new byte[ArgCount];
                Array.Copy(reply, ArgOffset, args, 0, ArgCount);

                if (status == StatusTimeout) return null;
                if (status == StatusUnsupported) return null;
                if (status != StatusOk) continue;

                return new Reply(status, args);
            }
        }

        return null;
    }

    private static byte[] BuildRequest(byte transactionId, byte command)
    {
        var buffer = new byte[ReportSize];
        buffer[BodyOffset + 1] = transactionId;
        buffer[BodyOffset + 5] = 0x02;
        buffer[BodyOffset + 6] = CommandClass;
        buffer[BodyOffset + 7] = command;

        byte checksum = 0;
        for (int i = BodyOffset + 2; i < BodyOffset + 88; i++) checksum ^= buffer[i];
        buffer[BodyOffset + 88] = checksum;

        return buffer;
    }
}
