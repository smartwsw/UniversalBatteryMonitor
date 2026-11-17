using System.Text.Json;
using System.Text.Json.Serialization;

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
                var cfg = JsonSerializer.Deserialize(File.ReadAllText(ConfigPath), ConfigJsonContext.Default.AppConfig);
                if (cfg is not null)
                {
                    if (cfg.IntervalSeconds < 10) cfg.IntervalSeconds = 60;
                    cfg.DeviceOrder ??= new List<string>();
                    return cfg;
                }
            }
        }
        catch (Exception e)
        {
            Log("读取配置失败", e);
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(this, ConfigJsonContext.Default.AppConfig));
        }
        catch (Exception e)
        {
            Log("保存配置失败", e);
        }
    }

    /// <summary>
    /// 配置写入失败时留个痕迹。以前这里是空 catch，
    /// 裁剪发布下序列化被禁用导致保存静默失效，拖拽顺序永远存不下来也查不出原因。
    /// </summary>
    private static void Log(string what, Exception e)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.AppendAllText(Path.Combine(Dir, "app.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {what}: {e.GetType().Name}: {e.Message}{Environment.NewLine}");
        }
        catch
        {
            // 连日志都写不了就只能放弃，不能因为写日志再抛一次
        }
    }
}

/// <summary>
/// 源生成的序列化上下文。裁剪发布默认禁用了基于反射的 JSON 序列化，
/// 直接用 JsonSerializer.Serialize(object) 会抛 InvalidOperationException，
/// 靠源生成器才能在裁剪后正常工作。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext
{
}
