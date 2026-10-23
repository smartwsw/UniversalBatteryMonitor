using Microsoft.Win32;

namespace BatteryMonitor.App;

/// <summary>
/// 开机自启：HKCU Run 项。任务管理器「启动」标签能看到并可启用/禁用。
/// 程序只读真实状态，绝不在用户没点的情况下改动系统设置。
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "BatteryMonitor";

    public static string? ExePath => Environment.ProcessPath;

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            if (key?.GetValue(ValueName) is not string value || value.Length == 0) return false;

            // 被任务管理器禁用时 approved 首字节为 0x03
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, false);
            if (approved?.GetValue(ValueName) is byte[] data && data.Length > 0)
                return data[0] != 0x03;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                string? exe = ExePath;
                if (string.IsNullOrEmpty(exe)) return false;

                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                key?.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
                SetApproved(true);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.DeleteValue(ValueName, false);
                SetApproved(false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetApproved(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ApprovedKey, true);
            var data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            key?.SetValue(ValueName, data, RegistryValueKind.Binary);
        }
        catch
        {
            // 任务管理器状态只是锦上添花，失败不影响自启本身
        }
    }
}
