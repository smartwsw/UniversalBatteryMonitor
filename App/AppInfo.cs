namespace BatteryMonitor.App;

internal static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    /// <summary>面板标题栏和菜单里显示的完整标识。</summary>
    public static string Title => $"设备电量 {Version}";

    private static string ReadVersion()
    {
        var assembly = typeof(AppInfo).Assembly;
        string? informational = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // 带上源码版本号时只保留数字段，界面上不做长串展示
            int plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
