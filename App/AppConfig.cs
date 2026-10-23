using System.Text.Json;

namespace BatteryMonitor.App;

internal sealed class AppConfig
{
    public int IntervalSeconds { get; set; } = 60;
    public bool ShowTray { get; set; } = true;

    /// <summary>设备在列表里的显示顺序（存 Reading.Key）。</summary>
    public List<string> DeviceOrder { get; set; } = new();

    public static readonly (int Seconds, string Label)[] IntervalChoices =
    {
        (30, "30 秒"), (60, "1 分钟"), (120, "2 分钟"),
        (300, "5 分钟"), (600, "10 分钟"), (1800, "30 分钟"),
    };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryMonitor");

    public static string ConfigPath => Path.Combine(Dir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                if (cfg is not null)
                {
                    if (cfg.IntervalSeconds < 10) cfg.IntervalSeconds = 60;
                    cfg.DeviceOrder ??= new List<string>();
                    return cfg;
                }
            }
        }
        catch
        {
            // 配置损坏就用默认值，不影响运行
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 存不下就算了，不打断使用
        }
    }
}
