using BatteryMonitor.Hid;

namespace BatteryMonitor.Devices;

/// <summary>
/// ASUS ROG OMNI RECEIVER（0x0B05:0x1ACE）。
/// 接收器在同一个 mi_02 下开了三个厂商页，各管一类设备：
///   FF02（col01） 配对列表：写 01 A0 00 00，回复从第 5 字节起每 4 字节一组设备 PID；
///   FF00（col02） 键盘：写 02 12 01，回复第 6 字节为电量、第 9 字节为充电标志；
///   FF01（col03） 鼠标：写 03 12 07，回复第 5 字节为电量、第 10 字节为充电标志。
/// 产品名靠配对列表里的 PID 反查，接收器本身只报 "ROG OMNI RECEIVER"。
/// </summary>
internal static class AsusOmniProtocol
{
    public const int Vid = 0x0B05;
    public const int Pid = 0x1ACE;

    private const int ReportSize = 64;

    private const int PagePairing = 0xFF02;
    private const int PageKeyboard = 0xFF00;
    private const int PageMouse = 0xFF01;

    private static readonly (int Pid, string Name, bool IsKeyboard)[] KnownProducts =
    {
        (0x1A85, "ROG Azoth", true),
        (0x1B42, "ROG Azoth Extreme", true),
        (0x1CF1, "ROG Azoth Extreme SE", true),
        (0x1AB0, "ROG Strix Scope II 96", true),
        (0x1B7A, "ROG Strix Scope II 96 RX", true),
        (0x1B06, "ROG Falchion RX Low Profile", true),
        (0x1B65, "ROG Harpe Ace Mini", false),
        (0x1C0E, "ROG Keris II Origin", false),
        (0x1D4E, "ROG Keris II Origin KJP", false),
        (0x1A94, "ROG Harpe Ace Aim Lab Edition", false),
        (0x1AD7, "ROG Strix Impact III Wireless", false),
        (0x1A72, "ROG Gladius III Aimpoint", false),
        (0x1A68, "ROG Keris Wireless Aimpoint", false),
        (0x1A6A, "ROG Keris Wireless Aimpoint", false),
        (0x1B1A, "ROG Keris II Ace", false),
        (0x1B18, "ROG Keris II Ace", false),
        (0x1B68, "ROG Harpe Ace Extreme", false),
        (0x1B69, "ROG Harpe Ace Extreme", false),
    };

    public sealed class PairedDevice
    {
        public int ProductId { get; init; }
        public string Name { get; init; } = "";
        public bool IsKeyboard { get; init; }
        public int Index { get; init; }
    }

    public sealed class SlotReading
    {
        public int Index { get; init; }
        public string Name { get; init; } = "";
        public bool IsKeyboard { get; init; }
        public int? Level { get; init; }
        public bool Charging { get; init; }
        public string Error { get; init; } = "";
        public byte[] Raw { get; init; } = Array.Empty<byte>();
    }

    public static bool IsSupported(List<HidInterface> group) => FindByPage(group, PagePairing) is not null;

    /// <summary>接收器本体所在的分组。</summary>
    public static HidInterface? FindInterface(List<HidInterface> group) => FindByPage(group, PagePairing);

    private static HidInterface? FindByPage(List<HidInterface> group, int page) =>
        group.FirstOrDefault(d => d.Vid == Vid && d.Pid == Pid &&
                                  d.UsagePage == page && d.OutputLength >= ReportSize);

    /// <summary>读接收器的配对列表，得到挂了哪些设备。</summary>
    public static List<PairedDevice> ReadPairedDevices(List<HidInterface> group)
    {
        var pairing = FindByPage(group, PagePairing);
        if (pairing is null) return new List<PairedDevice>();

        byte[]? reply;
        try
        {
            using var dev = new HidDevice(pairing.Path, write: true);
            if (!dev.WriteAccess) return new List<PairedDevice>();

            dev.Drain();
            var request = new byte[ReportSize];
            request[0] = 0x01;
            request[1] = 0xA0;
            dev.WriteOutput(request);
            reply = ReadReply(dev, 1500);
        }
        catch (HidException)
        {
            return new List<PairedDevice>();
        }

        if (reply is null) return new List<PairedDevice>();

        var found = new List<PairedDevice>();
        int index = 0;
        for (int offset = 5; offset + 3 < reply.Length; offset += 4)
        {
            int pid = reply[offset] | (reply[offset + 1] << 8);
            if (pid == 0) break;

            var known = KnownProducts.FirstOrDefault(k => k.Pid == pid);
            found.Add(new PairedDevice
            {
                ProductId = pid,
                Name = known.Name ?? $"ROG 设备 0x{pid:X4}",
                IsKeyboard = known.IsKeyboard,
                Index = index++,
            });
        }

        return found;
    }

    /// <summary>读所有配对设备的电量。</summary>
    public static List<SlotReading> ReadAll(List<HidInterface> group)
    {
        var paired = ReadPairedDevices(group);
        if (paired.Count == 0) return new List<SlotReading>();

        var readings = new List<SlotReading>();
        var keyboard = FindByPage(group, PageKeyboard);
        var mouse = FindByPage(group, PageMouse);

        foreach (var device in paired)
        {
            var iface = device.IsKeyboard ? keyboard : mouse;
            if (iface is null)
            {
                readings.Add(new SlotReading
                {
                    Index = device.Index,
                    Name = device.Name,
                    IsKeyboard = device.IsKeyboard,
                    Error = "接收器上没有找到对应的接口",
                });
                continue;
            }

            readings.Add(device.IsKeyboard ? ReadKeyboard(iface, device) : ReadMouse(iface, device));
        }

        return readings;
    }

    private static SlotReading ReadKeyboard(HidInterface iface, PairedDevice device)
    {
        var (level, charging, raw, error) = Query(iface, 0x02, 0x12, 0x01, levelOffset: 6, chargingOffset: 9);
        return new SlotReading
        {
            Index = device.Index,
            Name = device.Name,
            IsKeyboard = true,
            Level = level,
            Charging = charging,
            Error = error,
            Raw = raw,
        };
    }

    private static SlotReading ReadMouse(HidInterface iface, PairedDevice device)
    {
        var (level, charging, raw, error) = Query(iface, 0x03, 0x12, 0x07, levelOffset: 5, chargingOffset: 10);
        return new SlotReading
        {
            Index = device.Index,
            Name = device.Name,
            IsKeyboard = false,
            Level = level,
            Charging = charging,
            Error = error,
            Raw = raw,
        };
    }

    private static (int? Level, bool Charging, byte[] Raw, string Error) Query(
        HidInterface iface, byte reportId, byte command, byte slot, int levelOffset, int chargingOffset)
    {
        try
        {
            using var dev = new HidDevice(iface.Path, write: true);
            if (!dev.WriteAccess)
                return (null, false, Array.Empty<byte>(), "没有写权限（接收器被其它程序占用）");

            for (int attempt = 0; attempt < 2; attempt++)
            {
                dev.Drain();
                var request = new byte[ReportSize];
                request[0] = reportId;
                request[1] = command;
                request[2] = slot;
                dev.WriteOutput(request);

                var reply = ReadReply(dev, 900,
                    r => r.Length > 2 && r[1] == command && r[2] == slot);
                if (reply is null) continue;

                if (reply.Length <= levelOffset) return (null, false, reply, "回复长度不足");

                int level = reply[levelOffset];
                if (level is <= 0 or > 100)
                    return (null, false, reply, $"设备在休眠或没有上报电量（该字节为 {level}）");

                bool charging = reply.Length > chargingOffset && reply[chargingOffset] > 0;
                return (level, charging, reply, "");
            }

            return (null, false, Array.Empty<byte>(), "设备没有回复（多半在休眠，动一下设备后重试）");
        }
        catch (HidException e)
        {
            return (null, false, Array.Empty<byte>(), $"收发失败：{e.Message}");
        }
    }

    private static byte[]? ReadReply(HidDevice dev, int timeoutMs, Func<byte[], bool>? match = null)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            var report = dev.ReadInput(120, ReportSize);
            if (report is null || report.Length < 3) continue;
            if (match is null || match(report)) return report;
        }

        return null;
    }
}
