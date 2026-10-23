using BatteryMonitor.App;
using BatteryMonitor.Devices;

namespace BatteryMonitor.Ui;

/// <summary>通知区域电量图标：只显示设备列表里排在第一位的那台外设。左键刷新、右键打开面板。</summary>
internal sealed class TrayManager : IDisposable
{
    private const int LowLevel = 20;

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SynchronizationContext _ui;
    private readonly Action _openUi;
    private readonly AppConfig _config;
    private readonly NotifyIcon _icon = new();

    /// <summary>已经为哪些设备发过低电量提醒，充上电或拔掉后清掉，避免每轮都弹。</summary>
    private readonly HashSet<string> _lowNotified = new();

    private Icon? _glyph;
    private bool _busy;
    private bool _disposed;

    public TrayManager(AppConfig config, Action openUi)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _config = config;
        _openUi = openUi;

        _icon.ContextMenuStrip = BuildMenu();
        // 单击刷新，双击打开面板
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) Refresh();
        };
        _icon.DoubleClick += (_, _) => _openUi();
        // Icon 必须先于 Visible 设置，否则托盘里出来的是空白图标
        _icon.Icon = BatteryIcon.Create(null);
        _icon.Text = "设备电量";
        _icon.Visible = true;

        _timer.Interval = Math.Max(10, _config.IntervalSeconds) * 1000;
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    public void ApplyInterval(int seconds)
    {
        _timer.Interval = Math.Max(10, seconds) * 1000;
        _timer.Start();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开面板", null, (_, _) => _openUi());
        menu.Items.Add("立即刷新", null, (_, _) => Refresh());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) =>
        {
            _timer.Stop();
            Dispose();
            Application.Exit();
        });
        return menu;
    }

    public void Refresh()
    {
        if (_busy || _disposed) return;
        _busy = true;

        Task.Run(() =>
        {
            List<DeviceLevel> levels;
            try
            {
                levels = DeviceReader.ReadLevels();
            }
            catch
            {
                levels = new List<DeviceLevel>();
            }

            _ui.Post(_ =>
            {
                if (_disposed) return;
                Apply(levels);
                _busy = false;
            }, null);
        });
    }

    private void Apply(List<DeviceLevel> levels)
    {
        var ordered = SortBySavedOrder(levels);
        var first = ordered.FirstOrDefault();
        if (first.Key is null)
        {
            SetIcon(null, "未检测到外设");
            return;
        }

        SetIcon(first.Level, BuildTooltip(ordered));
        NotifyLowLevels(ordered);
    }

    private static string BuildTooltip(List<DeviceLevel> levels)
    {
        var lines = levels.Take(MaxTipLines)
            .Select(l => l.Level is not null
                ? $"{Trim(l.Title)}：{l.Level}%"
                : $"{Trim(l.Title)}：读不到电量")
            .ToList();

        // 不列"另有 N 台"占掉第 4 行：提示框高度有限，多一行反而把前三行挤没了
        int room = 127 - $"最后更新 {DateTime.Now:HH:mm:ss}".Length - 1;
        var text = FitLines(lines, room);
        return text + "\n" + $"最后更新 {DateTime.Now:HH:mm:ss}";
    }

    private const int MaxTipLines = 3;

    /// <summary>NotifyIcon 的提示文本上限 127 字符，超了会被系统整条丢掉。</summary>
    private static string FitLines(List<string> lines, int budget)
    {
        while (lines.Count > 1 && string.Join("\n", lines).Length > budget)
            lines.RemoveAt(lines.Count - 1);
        var text = string.Join("\n", lines);
        return text.Length > budget ? text[..Math.Max(0, budget - 1)] + "…" : text;
    }

    private static string Trim(string title) =>
        title.Length <= 24 ? title : title[..23] + "…";

    /// <summary>电量掉到 20% 以下弹一次系统通知；充电或恢复后重新武装。</summary>
    private void NotifyLowLevels(List<DeviceLevel> levels)
    {
        var present = levels.Select(l => l.Key).ToHashSet();

        foreach (var device in levels)
        {
            bool low = device.Level is int level and > 0 and < LowLevel;
            if (!low || device.Charging)
            {
                _lowNotified.Remove(device.Key);
                continue;
            }

            if (!_lowNotified.Add(device.Key)) continue;

            _icon.BalloonTipTitle = "电量偏低";
            _icon.BalloonTipText = $"{device.Title} 只剩 {device.Level}%，该充电了。";
            _icon.BalloonTipIcon = ToolTipIcon.Warning;
            _icon.ShowBalloonTip(6000);
        }

        // 设备拔掉之后把记录清掉，下次插回来还能再提醒
        _lowNotified.RemoveWhere(key => !present.Contains(key));
    }

    private void SetIcon(int? level, string tip)
    {
        var glyph = BatteryIcon.Create(level);
        _icon.Icon = glyph;
        _glyph?.Dispose();
        _glyph = glyph;

        _icon.Text = tip;
    }

    private List<DeviceLevel> SortBySavedOrder(List<DeviceLevel> levels)
    {
        var order = _config.DeviceOrder;
        if (order.Count == 0) return levels;

        var rank = new Dictionary<string, int>();
        for (int i = 0; i < order.Count; i++) rank[order[i]] = i;

        return levels
            .OrderBy(l => rank.TryGetValue(l.Key, out int i) ? i : int.MaxValue)
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();

        _icon.Visible = false;
        _icon.Dispose();
        _glyph?.Dispose();
    }
}
