using BatteryMonitor.App;
using BatteryMonitor.Devices;
using BatteryMonitor.Ui;

namespace BatteryMonitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var config = AppConfig.Load();

        if (args.Any(a => a.Equals("--once", StringComparison.OrdinalIgnoreCase)))
        {
            RunOnce();
            return;
        }

        if (args.Any(a => a.Equals("--list", StringComparison.OrdinalIgnoreCase)))
        {
            ListHid();
            return;
        }

        if (args.Any(a => a.Equals("--version", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(AppInfo.Title);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // 未处理异常默认会弹 WinForms 的错误框，一闪而过且留不下线索。
        // 这里统一接管并写到 %LOCALAPPDATA%\BatteryMonitor\crash.log，
        // 出问题时能直接看到堆栈，而不是只看到一个闪过的窗口。
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogException("UI 线程", e.Exception);
        AppDomain.CurrentDomain.UnhandledException +=
            (_, e) => LogException("域", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("后台任务", e.Exception);
            e.SetObserved();
        };

        var form = new MainForm(config);
        TrayManager? tray = null;

        if (config.ShowTray)
        {
            tray = new TrayManager(config, () => ShowForm(form));
            form.SetTrayHook(enabled =>
            {
                if (enabled)
                {
                    tray ??= new TrayManager(config, () => ShowForm(form));
                }
                else
                {
                    tray?.Dispose();
                    tray = null;
                }
            });
            form.OrderChanged = () => tray?.Refresh();
        }

        form.FormClosed += (_, _) =>
        {
            tray?.Dispose();
            Application.Exit();
        };

        bool startHidden = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        if (startHidden) Application.Run(new ApplicationContext(form));
        else Application.Run(form);
    }

    private static void LogException(string source, Exception? ex)
    {
        if (ex is null) return;
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BatteryMonitor", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}:{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志写不下去也不能再抛，否则又是一层错误框
        }
    }

    private static void ShowForm(MainForm form)
    {
        if (form.IsDisposed) return;
        form.Show();
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.Activate();
        form.BringToFront();
    }

    private static void ListHid()
    {
        var all = Hid.HidEnumerator.Enumerate();
        Console.WriteLine(AppInfo.Title);
        Console.WriteLine($"共 {all.Count} 个 HID 接口");
        foreach (var d in all.OrderBy(d => d.Vid).ThenBy(d => d.Pid).ThenBy(d => d.UsagePage))
        {
            Console.WriteLine($"{d.VidPid}  {d.UsagePage:X4}:{d.Usage:X4}  " +
                              $"in={d.InputLength,3} out={d.OutputLength,3} feat={d.FeatureLength,3}  {d.DisplayName}");
            if (d.Error.Length > 0) Console.WriteLine($"    {d.Error}");
        }
    }

    private static void RunOnce()
    {
        Console.WriteLine(AppInfo.Title);
        Console.WriteLine();

        foreach (var r in DeviceReader.ReadAll())
        {
            if (r.Ok)
            {
                Console.WriteLine($"{r.Title}: {r.Level}%   {r.Detail}");
                if (r.Extra.Length > 0) Console.WriteLine($"    {r.Extra}");
            }
            else
            {
                Console.WriteLine($"{r.Title}: 读取失败 —— {r.Error}");
            }
        }
    }
}
