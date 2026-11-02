using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace BatteryMonitor.App;

/// <summary>
/// Razer 接收器的控制接口（mi_00）只允许管理员进程打开，普通启动会拿到 err=5。
/// 这里只做检测和"以管理员身份重启"，不自动提权，避免开机自启时静默弹 UAC。
/// </summary>
internal static class Elevation
{
    public static bool IsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>以管理员权限重启当前进程，成功返回 true（调用方随即可退出）。</summary>
    public static bool RestartElevated(out string error)
    {
        error = "";

        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            error = "拿不到当前程序路径";
            return false;
        }

        var startInfo = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };

        foreach (string arg in Environment.GetCommandLineArgs().Skip(1)) startInfo.ArgumentList.Add(arg);

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            error = "已取消管理员授权";
            return false;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
