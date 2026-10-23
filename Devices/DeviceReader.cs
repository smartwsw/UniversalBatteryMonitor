using BatteryMonitor.Hid;

namespace BatteryMonitor.Devices;

internal sealed class Reading
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; set; } = "";
    public int? Level { get; set; }
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Error { get; set; } = "";
    public string Extra { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool Ok => Level is not null;
}

internal static class DeviceReader
{
    /// <summary>本机 HID 枚举结果里的一种已知设备类型。</summary>
    private sealed class DeviceKind
    {
        public string Id { get; init; } = "";
        public string DefaultTitle { get; init; } = "";
        public Func<List<HidInterface>, bool> Applies { get; init; } = _ => false;
        public Func<List<HidInterface>, HidInterface?> Primary { get; init; } = _ => null;
        public Func<List<HidInterface>, BatteryResult?> Read { get; init; } = _ => null;

        /// <summary>一个接收器挂多台设备时用它产出多行；返回 null 表示按单设备处理。</summary>
        public Func<List<HidInterface>, List<Reading>?>? ReadMany { get; init; }
    }

    private static readonly List<DeviceKind> Kinds = new()
    {
        new DeviceKind
        {
            Id = "asus-omni",
            DefaultTitle = "ROG OMNI 无线设备",
            Applies = AsusOmniProtocol.IsSupported,
            Primary = AsusOmniProtocol.FindInterface,
            Read = g =>
            {
                var iface = AsusOmniProtocol.FindInterface(g);
                if (iface is null) return null;

                return BatteryResult.Fail("ROG OMNI 接收器",
                    "接收器没有响应配对列表查询（确认接收器插稳）");
            },
            ReadMany = g => ReadOmniRows(g),
        },
        new DeviceKind
        {
            Id = "steelseries",
            DefaultTitle = "SteelSeries 无线耳机",
            Applies = g => SteelSeriesProtocol.FindInterface(g) is not null,
            Primary = SteelSeriesProtocol.FindInterface,
            Read = SteelSeriesProtocol.ReadBattery,
        },
        new DeviceKind
        {
            Id = "razer",
            DefaultTitle = "Razer 无线外设",
            Applies = g => RazerProtocol.IsSupported(g),
            Primary = RazerProtocol.FindControlInterface,
            Read = RazerProtocol.ReadBattery,
        },
        new DeviceKind
        {
            Id = "standard",
            DefaultTitle = "HID 电量设备",
            Applies = g => g.Any(BatteryReader.HasBatteryUsage),
            Primary = g => g.FirstOrDefault(BatteryReader.HasBatteryUsage),
            Read = g => g.Select(BatteryReader.TryStandardUsage).FirstOrDefault(r => r is { Ok: true })
                       ?? g.Select(BatteryReader.TryStandardUsage).FirstOrDefault(r => r is not null),
        },
    };

    public static List<Reading> ReadAll()
    {
        var readings = new List<Reading>();

        // 一台物理设备可能暴露多个 HID 接口，按物理分组后只出一行
        foreach (var group in HidEnumerator.Enumerate()
                     .GroupBy(d => d.Group)
                     .Select(g => g.ToList()))
        {
            // XInput 手柄走 XInput API，没有厂商 HID 通道，别再用 HID 协议去试
            if (XInputBattery.IsXInputGroup(group))
            {
                var pads = XInputBattery.ReadAll(group[0].Product.Length > 0
                    ? group[0].Product
                    : "XInput 无线手柄");
                if (pads.Count > 0) readings.AddRange(pads);
                else readings.Add(new Reading
                {
                    Key = $"{group[0].Group}|xinput",
                    Title = group[0].Product.Length > 0 ? group[0].Product : "XInput 无线手柄",
                    Subtitle = "045E:028E · XInput",
                    Error = "手柄已连接但由 USB 供电，没有电池电量",
                });
                continue;
            }

            foreach (var kind in Kinds.Where(k => k.Applies(group)))
            {
                var many = kind.ReadMany?.Invoke(group);
                if (many is not null) readings.AddRange(many);
                else readings.Add(Read(kind, group));
            }
        }

        if (readings.Count == 0)
        {
            readings.Add(new Reading
            {
                Key = "none",
                Title = "未检测到外设",
                Subtitle = "本机 HID 列表里没有可读取电量的设备",
                Error = "插上无线接收器后点“刷新”",
            });
        }

        return readings;
    }

    /// <summary>托盘用的轻量视图。</summary>
    public static List<DeviceLevel> ReadLevels() =>
        ReadAll().Select(r => new DeviceLevel(r.Key, r.Title, r.Level, r.Status == "充电中",
            r.Error.Length > 0 ? r.Error : "读取失败")).ToList();

    /// <summary>ROG OMNI 一个接收器可以挂多台设备，按配对列表逐台出列表行。</summary>
    private static List<Reading>? ReadOmniRows(List<HidInterface> group)
    {
        var iface = AsusOmniProtocol.FindInterface(group);
        if (iface is null) return null;

        var found = AsusOmniProtocol.ReadAll(group);
        if (found.Count == 0) return null;      // 配对列表读不到就走单行失败提示

        return found.Select(d => new Reading
        {
            // 用配对索引而不是名称做 Key：两台同名设备不会撞键，排序也稳定
            Key = $"{group[0].Group}|asus-omni|{d.Index}",
            Title = $"{d.Name} (OMNI)",
            Subtitle = $"{iface.VidPid} · 2.4G{(d.IsKeyboard ? "键盘" : "鼠标")}通道",
            Level = d.Level,
            Status = d.Level is null ? "" : d.Charging ? "充电中" : "放电中",
            Detail = d.Level is not null
                ? $"{(d.IsKeyboard ? "键盘" : "鼠标")}通道 · 回报 {d.Level}%"
                : "",
            Extra = d.Raw.Length > 0
                ? string.Join(" ", d.Raw.Take(14).Select(b => b.ToString("X2")))
                : "",
            Error = d.Error,
        }).ToList();
    }

    private static Reading Read(DeviceKind kind, List<HidInterface> group)
    {
        var primary = kind.Primary(group);
        string title = string.IsNullOrWhiteSpace(primary?.Product) ? kind.DefaultTitle : primary!.Product;

        var reading = new Reading
        {
            Key = $"{group[0].Group}|{kind.Id}",
            Title = title,
        };

        if (primary is null)
        {
            reading.Subtitle = "接口不可用";
            reading.Error = "设备已连接，但没有可用的 HID 接口";
            return reading;
        }

        reading.Subtitle = $"{primary.VidPid} · {kind.DefaultTitle}";

        try
        {
            var result = kind.Read(group);
            if (result is null)
            {
                reading.Error = "没有可用的电量读取方式";
                return reading;
            }

            if (!result.Ok)
            {
                reading.Error = result.Detail.Length > 0 ? result.Detail : "读取失败";
                return reading;
            }

            reading.Level = result.Level;
            reading.Status = result.StatusText;
            reading.Detail = result.Source;
            reading.Extra = result.Detail;
        }
        catch (Exception e)
        {
            reading.Error = $"读取出错：{e.Message}";
        }

        return reading;
    }
}
