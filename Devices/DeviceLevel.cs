namespace BatteryMonitor.Devices;

/// <summary>托盘用的轻量读数：不包含原始报文和详情文本。</summary>
internal readonly record struct DeviceLevel(
    string Key, string Title, int? Level, bool Charging, string Error);
