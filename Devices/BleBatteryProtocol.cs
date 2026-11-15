using System.Globalization;
using System.Text.RegularExpressions;
using BatteryMonitor.Hid;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace BatteryMonitor.Devices;

/// <summary>
/// 蓝牙低功耗设备的电量走 GATT Battery Service（0x180F / 0x2A19），
/// 和 HID 报文是两条互不相通的路：这两个服务不会出现在 SetupAPI 的 HID 枚举结果里，
/// 只能通过 WinRT 连上设备直接读。
/// 这里只处理 HID over GATT（0x1812）的设备——它们本身就在 HID 列表里，
/// 可以顺带从路径里拿到蓝牙地址。设备切回 2.4G 或关机时 HID 接口会消失，
/// 不会留下幽灵行。
/// </summary>
internal static class BleBatteryProtocol
{
    private const string HidServiceTag = "00001812-0000-1000-8000-00805f9b34fb";

    private static readonly Guid BatteryService = new("0000180F-0000-1000-8000-00805F9B34FB");
    private static readonly Guid BatteryLevel = new("00002A19-0000-1000-8000-00805F9B34FB");

    private static readonly Regex AddressPattern = new("_rev&[0-9a-f]{4}_([0-9a-f]{12})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly TimeSpan GattTimeout = TimeSpan.FromSeconds(8);

    public static bool IsSupported(List<HidInterface> group) =>
        group.Any(d => d.Path.Contains(HidServiceTag, StringComparison.OrdinalIgnoreCase));

    /// <summary>空列表表示这个设备当前不该出现在列表里（例如已切回 2.4G 但节点还在）。</summary>
    public static List<Reading>? ReadRows(List<HidInterface> group)
    {
        if (!TryGetAddress(group, out ulong address))
            return new List<Reading> { Row(group, "蓝牙外设", "无法从设备路径解析蓝牙地址") };

        var row = ReadCore(group, address).GetAwaiter().GetResult();
        return row is null ? new List<Reading>() : new List<Reading> { row };
    }

    private static async Task<Reading?> ReadCore(List<HidInterface> group, ulong address)
    {
        BluetoothLEDevice? device;
        try
        {
            device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        }
        catch
        {
            return null;
        }

        if (device is null) return null;

        using (device)
        {
            // 两次刷新之间设备刚好断开时，这一轮不出行，等它连回来
            if (device.ConnectionStatus != BluetoothConnectionStatus.Connected) return null;

            string name = string.IsNullOrWhiteSpace(device.Name) ? "蓝牙外设" : device.Name;

            var services = await Guard(() => device.GetGattServicesForUuidAsync(BatteryService));
            if (services is null) return Row(group, name, "蓝牙设备没有响应电量查询（多半已经断开或进入休眠）");

            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
                return Row(group, name, "该蓝牙设备没有暴露标准电量服务（0x180F）");

            foreach (var service in services.Services)
            {
                var characteristics = await Guard(() => service.GetCharacteristicsAsync());
                var level = characteristics?.Characteristics.FirstOrDefault(c => c.Uuid == BatteryLevel);
                if (level is null) continue;

                var read = await Guard(() => level.ReadValueAsync());
                if (read?.Value is null || read.Value.Length == 0)
                    return Row(group, name, $"电量特征值读取失败（{read?.Status.ToString() ?? "超时"}）");

                var buffer = new byte[read.Value.Length];
                DataReader.FromBuffer(read.Value).ReadBytes(buffer);
                int raw = buffer[0];

                return new Reading
                {
                    Key = $"{group[0].Group}|ble",
                    Title = name,
                    Subtitle = $"{group[0].VidPid} · 蓝牙",
                    Level = Math.Clamp(raw, 0, 100),
                    Detail = "蓝牙电量服务",
                    Extra = $"0x180F/0x2A19 → {raw}",
                };
            }

            return Row(group, name, "该蓝牙设备没有电量特征值（0x2A19）");
        }
    }

    /// <summary>
    /// 蓝牙栈偶尔会卡在 GATT 查询上不返回，兜一个超时，
    /// 免得一台休眠设备拖住整轮刷新。
    /// </summary>
    private static async Task<T?> Guard<T>(Func<IAsyncOperation<T>> start) where T : class
    {
        var task = Task.Run(async () => await start());
        if (await Task.WhenAny(task, Task.Delay(GattTimeout)) == task)
            return await task;

        _ = task.ContinueWith(t => t.Exception, TaskScheduler.Default);
        return null;
    }

    private static Reading Row(List<HidInterface> group, string name, string error) => new()
    {
        Key = $"{group[0].Group}|ble",
        Title = name,
        Subtitle = $"{group[0].VidPid} · 蓝牙",
        Error = error,
    };

    private static bool TryGetAddress(List<HidInterface> group, out ulong address)
    {
        foreach (var iface in group)
        {
            var match = AddressPattern.Match(iface.Path);
            if (!match.Success) continue;

            if (ulong.TryParse(match.Groups[1].Value, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out address))
                return true;
        }

        address = 0;
        return false;
    }
}
