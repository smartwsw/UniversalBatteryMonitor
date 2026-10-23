using System.Runtime.InteropServices;

namespace BatteryMonitor.Devices;

/// <summary>
/// XInput 无线手柄（飞智 VADER4 用 Xbox 兼容模式接入时是 045E:028E）。
/// 这类设备只暴露 XInput 通道、没有厂商 HID 页，电量只能问 XInput，
/// 而且返回的是 5 档而不是百分比。
/// </summary>
internal static class XInputBattery
{
    public const int Vid = 0x045E;
    public const int Pid = 0x028E;

    private const int ErrorSuccess = 0;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int GetState(uint index, out XInputState state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern int GetBattery(uint index, byte deviceType, out XInputBatteryInformation info);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX, LeftThumbY, RightThumbX, RightThumbY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    // BatteryType: 0 断开 / 1 有线 / 2 碱性 / 3 镍氢 / 4 未知
    private const byte TypeDisconnected = 0;
    private const byte TypeWired = 1;

    // BatteryLevel: 0 空 / 1 空 / 2 低 / 3 中 / 4 满 / 5 未知
    private static readonly int?[] LevelPercent = { null, null, 15, 40, 70, 100 };

    /// <summary>本机接的 XInput 手柄的显示名（从 HID 枚举里拿不到，用固定名）。</summary>
    private const string DefaultTitle = "XInput 无线手柄";

    /// <summary>某个 HID 分组是否就是 XInput 手柄（这类分组交给 XInput 处理，别再套 HID 协议）。</summary>
    public static bool IsXInputGroup(List<Hid.HidInterface> group) =>
        group.Any(d => d.Vid == Vid && d.Pid == Pid);

    /// <summary>读出所有已连接、靠电池供电的 XInput 手柄。没有则返回空列表。</summary>
    public static List<Reading> ReadAll(string title)
    {
        var readings = new List<Reading>();

        for (uint index = 0; index < 4; index++)
        {
            if (GetState(index, out _) != ErrorSuccess) continue;
            if (GetBattery(index, 0, out var battery) != ErrorSuccess) continue;
            if (battery.BatteryType == TypeDisconnected) continue;

            int? percent = battery.BatteryLevel <= 5 ? LevelPercent[battery.BatteryLevel] : null;

            // 飞智这类手柄插着线时 XInput 报 Type=1（规范上表示“有线、无电池”），
            // 但电量档位仍在更新，所以有线时不隐藏电量，只在状态上标出来
            bool wired = battery.BatteryType == TypeWired;

            readings.Add(new Reading
            {
                Key = $"xinput|{index}",
                Title = title,
                Subtitle = $"045E:028E · XInput 手柄 {index}{(wired ? " · 有线" : "")}",
                Level = percent,
                Status = percent is null ? "" : wired ? "有线连接" : "放电中",
                Detail = percent is null ? "" : $"XInput 电量档位 {battery.BatteryLevel}/4",
                Error = percent is null ? "手柄没有上报电量（XInput 返回未知档位）" : "",
            });
        }

        return readings;
    }
}
