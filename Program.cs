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

        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

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
