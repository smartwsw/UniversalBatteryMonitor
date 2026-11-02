using System.Diagnostics;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace BatteryMonitor.App;

/// <summary>
/// 开机自启。两种模式：
/// 1. HKCU Run 项（普通权限启动，读不到雷蛇）；
/// 2. 计划任务 RunLevel=Highest（开机直接以管理员权限启动，不弹 UAC）——雷蛇需要这一种。
/// 程序只读真实状态，用户没点就不改系统设置。
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "BatteryMonitor";
    private const string TaskName = "BatteryMonitor";

    public static string? ExePath => Environment.ProcessPath;

    /// <summary>哪种方式开着（计划任务优先，它会以管理员权限启动）。</summary>
    public static bool IsEnabled() => IsTaskEnabled() || IsRunKeyEnabled();

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                // 想提权自启就必须注册 Highest 计划任务，普通权限下注册不了
                if (Elevation.IsAdmin()) return CreateTask();

                RemoveTask();
                return SetRunKey(true);
            }

            RemoveTask();
            return SetRunKey(false);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTaskEnabled()
    {
        var (code, _) = RunSchTasks($"/Query /TN \"{TaskName}\"");
        return code == 0;
    }

    private static bool IsRunKeyEnabled()
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

    private static bool CreateTask()
    {
        string? exe = ExePath;
        if (string.IsNullOrEmpty(exe)) return false;

        string sid = SecurityIdentifier();

        string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
            "<Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
            "<RegistrationInfo><Description>开机启动设备电量监控（管理员权限）</Description></RegistrationInfo>" +
            "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + sid + "</UserId></LogonTrigger></Triggers>" +
            "<Principals><Principal id=\"Author\"><UserId>" + sid + "</UserId>" +
            "<LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
            "<Settings>" +
            "<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>" +
            "<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
            "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
            "<AllowHardTerminate>true</AllowHardTerminate>" +
            "<StartWhenAvailable>false</StartWhenAvailable>" +
            "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>" +
            "<IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
            "<AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden>" +
            "<RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>" +
            "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority>" +
            "</Settings>" +
            "<Actions Context=\"Author\"><Exec><Command>\"" + exe + "\"</Command>" +
            "<Arguments>--tray</Arguments>" +
            "<WorkingDirectory>" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + "</WorkingDirectory>" +
            "</Exec></Actions></Task>";

        string temp = Path.Combine(Path.GetTempPath(), $"battery-monitor-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(temp, xml, Encoding.Unicode);
            var (code, _) = RunSchTasks($"/Create /TN \"{TaskName}\" /XML \"{temp}\" /F");
            if (code != 0) return false;

            SetRunKey(false);   // 用任务就不要再留 Run 项，免得开两次
            return true;
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }

    private static void RemoveTask() => RunSchTasks($"/Delete /TN \"{TaskName}\" /F");

    private static bool SetRunKey(bool enabled)
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

    private static string SecurityIdentifier()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? Environment.UserName;
    }

    private static (int Code, string Output) RunSchTasks(string arguments)
    {
        var info = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(info);
        if (process is null) return (-1, "");

        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        return (process.HasExited ? process.ExitCode : -1, output);
    }
}
