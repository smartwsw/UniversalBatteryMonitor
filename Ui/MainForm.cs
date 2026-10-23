using System.Drawing;
using System.Drawing.Drawing2D;
using BatteryMonitor.App;
using BatteryMonitor.Devices;

namespace BatteryMonitor.Ui;

internal sealed class MainForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(24, 25, 28);
    private static readonly Color PanelColor = Color.FromArgb(32, 34, 38);
    private static readonly Color PanelAlt = Color.FromArgb(42, 45, 50);
    private static readonly Color FgColor = Color.FromArgb(232, 234, 237);
    private static readonly Color MutedColor = Color.FromArgb(150, 156, 164);
    private static readonly Color BorderColor = Color.FromArgb(52, 56, 62);
    private static readonly Color AccentColor = Color.FromArgb(79, 140, 255);

    private Panel _listHost = null!;
    private FlowLayoutPanel _list = null!;
    private SlimScrollBar _scroll = null!;
    private int _scrollOffset;
    private Button _refreshButton = null!;
    private Label _footer = null!;
    private CheckBox _autoCheck = null!;
    private CheckBox _trayCheck = null!;
    private CheckBox _bootCheck = null!;
    private ComboBox _intervalBox = null!;

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Dictionary<string, DeviceRow> _rows = new();
    private AppConfig _config;
    private bool _busy;
    private bool _bootGuarding;
    private Action<bool>? _onTrayToggle;

    private DeviceRow? _dragRow;
    private Point _dragStart;
    private bool _dragMoved;

    public MainForm(AppConfig config)
    {
        _config = config;

        Text = "设备电量";
        // 标题栏和任务栏用嵌入的 app.ico，托盘图标仍按电量现画
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor = BgColor;
        ForeColor = FgColor;
        Font = new Font("Microsoft YaHei UI", 10f);
        ClientSize = new Size(540, 620);
        MinimumSize = new Size(460, 420);
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BgColor,
            Padding = new Padding(16, 12, 16, 10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        // 列表放在自己算滚动的容器里，这样能用细窄的深色滚动条，而不是系统灰条
        _listHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            Margin = new Padding(0),
        };
        root.Controls.Add(_listHost, 0, 1);

        _list = new FlowLayoutPanel
        {
            Location = new Point(0, 0),
            AutoSize = false,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = BgColor,
            Padding = new Padding(0, 10, 0, 8),
        };
        _listHost.Controls.Add(_list);

        _scroll = new SlimScrollBar();
        _listHost.Controls.Add(_scroll);
        _scroll.Scrolled += offset =>
        {
            _scrollOffset = offset;
            _list.Top = -offset;
        };

        _listHost.Resize += (_, _) => Relayout();
        // WinForms 只会把滚轮发给有焦点的控件，鼠标停在卡片上时收不到，用消息过滤器接管
        Application.AddMessageFilter(new WheelRouter(this));

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            BackColor = BgColor,
        };
        bottom.Controls.Add(BuildSettings(), 0, 0);

        _footer = new Label
        {
            Text = "准备就绪",
            ForeColor = MutedColor,
            AutoSize = true,
            Margin = new Padding(2, 6, 0, 0),
        };
        bottom.Controls.Add(_footer, 0, 1);
        root.Controls.Add(bottom, 0, 2);

        _timer.Interval = _config.IntervalSeconds * 1000;
        _timer.Tick += (_, _) => RefreshAsync();

        Shown += (_, _) =>
        {
            // 这时候 Width/Height 才含边框，定位不会差出几十像素
            PlaceBottomRight();
            RefreshAsync();
            ScheduleTimer();
        };
    }

    /// <summary>默认开在任务栏上方、屏幕右下角（多显示器时用主屏）。</summary>
    private void PlaceBottomRight()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Location = new Point(
            Math.Max(area.Left, area.Right - Width - 24),
            Math.Max(area.Top, area.Bottom - Height - 24));
    }

    /// <summary>拖动结束后通知外部（托盘）重新排定显示哪台设备。</summary>
    public Action? OrderChanged { get; set; }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = BgColor,
            Margin = new Padding(0),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        header.Controls.Add(new Label
        {
            Text = "设备电量",
            Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold),
            ForeColor = FgColor,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 0, 4),
        }, 0, 0);

        _autoCheck = new CheckBox
        {
            Text = "自动刷新",
            ForeColor = MutedColor,
            AutoSize = true,
            Checked = true,
            TabStop = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 10, 12, 0),
        };
        _autoCheck.CheckedChanged += (_, _) => ScheduleTimer();
        header.Controls.Add(_autoCheck, 1, 0);

        _refreshButton = new Button
        {
            Text = "刷新",
            FlatStyle = FlatStyle.Flat,
            BackColor = PanelAlt,
            ForeColor = FgColor,
            AutoSize = false,
            Size = new Size(72, 30),
            Font = new Font("Microsoft YaHei UI", 9.5f),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 4),
        };
        _refreshButton.FlatAppearance.BorderColor = BorderColor;
        _refreshButton.FlatAppearance.BorderSize = 1;
        _refreshButton.Click += (_, _) => RefreshAsync();
        header.Controls.Add(_refreshButton, 2, 0);

        return header;
    }

    private Control BuildSettings()
    {
        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 2,
            BackColor = PanelColor,
            Padding = new Padding(12, 9, 12, 9),
            Margin = new Padding(0, 4, 0, 0),
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        settings.Controls.Add(new Label
        {
            Text = "刷新间隔",
            ForeColor = MutedColor,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 5, 8, 0),
        }, 0, 0);

        _intervalBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = PanelAlt,
            ForeColor = FgColor,
            Width = 100,
            Margin = new Padding(0, 3, 0, 0),
        };
        _intervalBox.Items.AddRange(AppConfig.IntervalChoices.Select(c => (object)c.Label).ToArray());
        _intervalBox.SelectedIndex = Math.Max(0,
            Array.FindIndex(AppConfig.IntervalChoices, c => c.Seconds == _config.IntervalSeconds));
        _intervalBox.SelectedIndexChanged += (_, _) =>
        {
            _config.IntervalSeconds = AppConfig.IntervalChoices[_intervalBox.SelectedIndex].Seconds;
            _config.Save();
            ScheduleTimer();
        };
        settings.Controls.Add(_intervalBox, 1, 0);

        _bootCheck = new CheckBox
        {
            Text = "开机自启",
            ForeColor = FgColor,
            AutoSize = true,
            Checked = Autostart.IsEnabled(),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(12, 5, 0, 0),
        };
        _bootCheck.CheckedChanged += (_, _) =>
        {
            if (_bootGuarding) return;
            if (Autostart.SetEnabled(_bootCheck.Checked)) return;

            _bootGuarding = true;
            _bootCheck.Checked = Autostart.IsEnabled();
            _bootGuarding = false;
            MessageBox.Show(this, "设置开机自启失败，可能被安全软件拦截。", "开机自启",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        settings.Controls.Add(_bootCheck, 3, 0);

        _trayCheck = new CheckBox
        {
            Text = "关闭窗口时最小化到托盘",
            ForeColor = FgColor,
            AutoSize = true,
            Checked = _config.ShowTray,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 5, 0, 0),
        };
        _trayCheck.CheckedChanged += (_, _) =>
        {
            _config.ShowTray = _trayCheck.Checked;
            _config.Save();
            _onTrayToggle?.Invoke(_trayCheck.Checked);
        };
        settings.Controls.Add(_trayCheck, 0, 1);
        settings.SetColumnSpan(_trayCheck, 2);

        return settings;
    }

    public void SetTrayHook(Action<bool> hook) => _onTrayToggle = hook;

    private void ScheduleTimer()
    {
        _timer.Interval = Math.Max(10, _config.IntervalSeconds) * 1000;
        if (_autoCheck.Checked) _timer.Start();
        else _timer.Stop();
    }

    public void RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        _refreshButton.Enabled = false;
        _footer.Text = "正在读取…";

        Task.Run(() =>
        {
            List<Reading> readings;
            try
            {
                readings = DeviceReader.ReadAll();
            }
            catch (Exception e)
            {
                readings = new List<Reading>
                {
                    new() { Key = "error", Title = "读取异常", Error = e.Message },
                };
            }

            if (IsDisposed) return;
            BeginInvoke(() => ApplyReadings(readings));
        });
    }

    private void ApplyReadings(List<Reading> readings)
    {
        if (IsDisposed) return;

        var ordered = ApplySavedOrder(readings);

        foreach (var stale in _rows.Keys.Where(k => ordered.All(r => r.Key != k)).ToList())
        {
            _rows[stale].Dispose();
            _rows.Remove(stale);
        }

        foreach (var reading in ordered)
        {
            if (!_rows.TryGetValue(reading.Key, out var row))
            {
                row = new DeviceRow(reading.Title);
                row.HookDrag(
                    e => BeginDrag(row, e),
                    DragMove,
                    EndDrag);
                _rows[reading.Key] = row;
                _list.Controls.Add(row);
            }
            row.Update(reading);
        }

        // 控件顺序跟着排序结果走
        for (int i = 0; i < ordered.Count; i++)
            _list.Controls.SetChildIndex(_rows[ordered[i].Key], i);

        ResizeRows();

        int ok = ordered.Count(r => r.Ok);
        _footer.Text = $"更新于 {DateTime.Now:HH:mm:ss}    读取成功 {ok}/{ordered.Count} 个设备" +
                       (_autoCheck.Checked ? "" : "    （自动刷新已关闭）");

        _busy = false;
        _refreshButton.Enabled = true;
    }

    private List<Reading> ApplySavedOrder(List<Reading> readings)
    {
        if (_config.DeviceOrder.Count == 0) return readings;

        var rank = new Dictionary<string, int>();
        for (int i = 0; i < _config.DeviceOrder.Count; i++) rank[_config.DeviceOrder[i]] = i;

        return readings
            .OrderBy(r => rank.TryGetValue(r.Key, out int i) ? i : int.MaxValue)
            .ToList();
    }

    private void BeginDrag(DeviceRow row, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragRow = row;
        _dragStart = Cursor.Position;
        _dragMoved = false;
        row.Capture = true;
    }

    private void DragMove(MouseEventArgs e)
    {
        if (_dragRow is null || e.Button != MouseButtons.Left) return;

        if (!_dragMoved)
        {
            if (Math.Abs(Cursor.Position.Y - _dragStart.Y) < 6 &&
                Math.Abs(Cursor.Position.X - _dragStart.X) < 6) return;
            _dragMoved = true;
            _dragRow.Cursor = Cursors.SizeNS;
        }

        var point = _list.PointToClient(Cursor.Position);
        var target = _list.Controls.OfType<DeviceRow>()
                          .FirstOrDefault(r => r != _dragRow && r.Bounds.Contains(point));
        if (target is null) return;

        int from = _list.Controls.GetChildIndex(_dragRow);
        int to = _list.Controls.GetChildIndex(target);
        if (from < 0 || to < 0 || from == to) return;

        _list.Controls.SetChildIndex(_dragRow, to);
    }

    private void EndDrag(MouseEventArgs e)
    {
        var row = _dragRow;
        if (row is null) return;

        row.Capture = false;
        row.Cursor = Cursors.SizeNS;
        _dragRow = null;

        if (!_dragMoved) return;
        _dragMoved = false;

        _config.DeviceOrder = _list.Controls.OfType<DeviceRow>()
            .Select(r => _rows.FirstOrDefault(p => p.Value == r).Key)
            .Where(k => k is not null)
            .ToList()!;
        _config.Save();
        OrderChanged?.Invoke();

        // 抓取时的高亮框在重排后可能留在原地，重绘一次
        _list.Invalidate();
    }

    private void ResizeRows()
    {
        Relayout();
    }

    /// <summary>鼠标落在设备列表上时，把滚轮消息转给内部滚动条。</summary>
    private sealed class WheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        private readonly MainForm _form;

        public WheelRouter(MainForm form) => _form = form;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if (_form.IsDisposed || !_form._listHost.IsHandleCreated) return false;

            var screen = new Point((short)((long)m.LParam & 0xFFFF),
                                   (short)(((long)m.LParam >> 16) & 0xFFFF));
            if (!_form._listHost.RectangleToScreen(_form._listHost.ClientRectangle).Contains(screen))
                return false;

            int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
            _form.ScrollByWheel(delta);
            return true;
        }
    }

    private void ScrollByWheel(int delta)
    {
        if (!_scroll.Visible) return;
        if (_scroll.ScrollBy(-delta / 120 * 64)) _scroll.Invalidate();
    }

    /// <summary>重算行宽和滚动范围：列表宽度跟着容器走，滚动条只在内容超出时出现。</summary>
    private void Relayout()
    {
        if (_listHost.IsDisposed) return;

        int barWidth = SlimScrollBar.Width_;
        int viewport = _listHost.ClientSize.Height;

        // 内容高度按行累加算：AutoSize 面板的 Height 要等布局跑完才更新，这里不能依赖它
        int content = _list.Padding.Vertical + _rows.Values.Sum(r => r.Height + r.Margin.Vertical);
        bool needsBar = content > viewport;

        // 有滚动条时列表要窄一点，否则文字会贴到滚动条下面
        int width = Math.Max(200, _listHost.ClientSize.Width - (needsBar ? barWidth : 0));
        foreach (var row in _rows.Values) row.Width = width;

        _scroll.Visible = needsBar;
        _scroll.SetBounds(_listHost.ClientSize.Width - barWidth, 0, barWidth, viewport);

        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, content - viewport));
        _scroll.Maximum = content;
        _scroll.Viewport = viewport;
        _scroll.Value = _scrollOffset;

        // 列表自己定高定宽，不靠 AutoSize：否则布局会反过来影响滚动偏移
        _list.SetBounds(0, -_scrollOffset, width, content);

        // 滚动内容要被容器裁掉，否则滚到底部时卡片会画到标题栏上
        _listHost.Invalidate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _config.ShowTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>纵向排列的设备行：左侧名称与状态，右侧大号电量数字和进度条。</summary>
internal sealed class DeviceRow : Panel
{
    private static readonly Color Muted = Color.FromArgb(150, 156, 164);
    private static readonly Color Fg = Color.FromArgb(232, 234, 237);
    private static readonly Color Sub = Color.FromArgb(124, 130, 138);
    private static readonly Color Card = Color.FromArgb(32, 34, 38);
    private static readonly Color Track = Color.FromArgb(46, 49, 55);

    private readonly TableLayoutPanel _grid;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _level;
    private readonly Label _status;
    private readonly LevelBar _bar;
    private readonly Label _detail;

    public DeviceRow(string title)
    {
        BackColor = Card;
        // 行高按内容实测给足：标题 44 + 数字 58 + 进度条 15 + 详情 22 + 上下留白 22
        Height = 166;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(16, 12, 16, 10);
        DoubleBuffered = true;

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = Card,
            Margin = new Padding(0),
        };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        // 每行都给定高度：AutoSize 行会把下面的详情标签压成半截
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 15));
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        Controls.Add(_grid);

        var caption = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card,
            Margin = new Padding(0),
        };
        caption.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        caption.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        _title = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
            ForeColor = Fg,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        };
        caption.Controls.Add(_title, 0, 0);

        _subtitle = new Label
        {
            Text = "正在读取…",
            ForeColor = Sub,
            AutoSize = true,
            Margin = new Padding(0),
        };
        caption.Controls.Add(_subtitle, 0, 1);
        _grid.Controls.Add(caption, 0, 0);

        _status = new Label
        {
            Text = "",
            ForeColor = Muted,
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(8, 6, 0, 0),
        };
        _grid.Controls.Add(_status, 1, 0);

        _level = new Label
        {
            Text = "--",
            Font = new Font("Microsoft YaHei UI", 30f, FontStyle.Bold),
            ForeColor = Muted,
            // 30pt 的 AutoSize 标签会被 AutoSize 行高裁掉，这里固定行高、按文本实测宽度
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Right,
            Size = new Size(140, 56),
            Margin = new Padding(8, 0, 0, 0),
        };
        _grid.Controls.Add(_level, 1, 1);

        _bar = new LevelBar { Height = 5, BackColor = Card, Dock = DockStyle.Fill };
        _bar.Margin = new Padding(0, 0, 0, 10);
        _grid.Controls.Add(_bar, 0, 2);
        _grid.SetColumnSpan(_bar, 2);

        _detail = new Label
        {
            Text = "",
            ForeColor = Sub,
            AutoSize = false,
            // 长文本用省略号收尾，而不是在右边缘留半截字
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        _grid.Controls.Add(_detail, 0, 3);
        _grid.SetColumnSpan(_detail, 2);

        FitTypography();
    }

    /// <summary>
    /// 行高按当前字体实测，而不是写死像素：高分屏上字体变大时文字才不会被裁掉。
    /// </summary>
    private void FitTypography()
    {
        int line = TextRenderer.MeasureText("Ag国", Font).Height;
        int caption = TextRenderer.MeasureText("Ag国", _title.Font).Height;
        int big = TextRenderer.MeasureText("100%", _level.Font).Height;

        _grid.RowStyles[0].Height = Math.Max(caption + line + 2, 40);
        _grid.RowStyles[1].Height = Math.Max(big, 40);
        _grid.RowStyles[2].Height = 15;
        _grid.RowStyles[3].Height = line + 4;

        _grid.ColumnStyles[1].Width = _level.Width;

        Height = (int)(_grid.RowStyles[0].Height
                       + _grid.RowStyles[1].Height
                       + _grid.RowStyles[2].Height
                       + _grid.RowStyles[3].Height)
                 + Padding.Vertical;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(52, 56, 62));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    /// <summary>整行都可拖动：子控件上的鼠标事件也交给行本身处理。</summary>
    internal void HookDrag(Action<MouseEventArgs> down, Action<MouseEventArgs> move,
                           Action<MouseEventArgs> up)
    {
        void Hook(Control control)
        {
            control.MouseDown += (_, e) => down(e);
            control.MouseMove += (_, e) => move(e);
            control.MouseUp += (_, e) => up(e);
            foreach (Control child in control.Controls) Hook(child);
        }

        Hook(this);
    }

    public void Update(Reading r)
    {
        _subtitle.Text = r.Subtitle;

        if (r.Ok)
        {
            _level.Text = $"{r.Level}%";
            _level.ForeColor = BatteryIcon.LevelColor(r.Level);
            _status.Text = r.Status is "未知" or "" ? "" : r.Status;
            _bar.Level = Math.Clamp(r.Level ?? 0, 0, 100);
            _bar.Color = BatteryIcon.LevelColor(r.Level);
            _detail.Text = r.Detail.Length > 0 ? r.Detail : r.Extra;
        }
        else
        {
            _level.Text = "--";
            _level.ForeColor = Muted;
            _status.Text = "";
            _bar.Level = 0;
            _bar.Color = Track;
            _detail.Text = r.Error;
        }

        FitLevelWidth();
        _bar.Invalidate();
    }

    /// <summary>大号数字按实测宽度收放，避免 100% 的百分号被右边缘切掉。</summary>
    private void FitLevelWidth()
    {
        int width = TextRenderer.MeasureText(_level.Text, _level.Font).Width + 8;
        if (width > _level.Width) _level.Width = width;
    }
}

/// <summary>深色细滚动条：只有一条圆角滑块，没有系统滚动条的箭头和凹槽。</summary>
internal sealed class SlimScrollBar : Control
{
    public const int Width_ = 8;

    private int _maximum;
    private int _viewport;
    private int _value;

    private bool _hover;
    private bool _dragging;
    private int _dragOffset;

    public SlimScrollBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(24, 25, 28);
        Width = Width_;
        Visible = false;
    }

    public event Action<int>? Scrolled;

    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(0, value); Track(); Invalidate(); }
    }

    public int Viewport
    {
        get => _viewport;
        set { _viewport = Math.Max(1, value); Track(); Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, _maximum - _viewport));
            if (clamped == _value) return;
            _value = clamped;
            Invalidate();
        }
    }

    private int ThumbHeight => Math.Max(24, (int)((long)_viewport * Height / Math.Max(1, _maximum)));
    private int ThumbTop  => Scrolling ? (int)((long)_value * (Height - ThumbHeight) / Math.Max(1, _maximum - _viewport)) : 0;
    private bool Scrolling => _maximum > _viewport;

    /// <summary>按像素滚动，返回是否真的动了。</summary>
    public bool ScrollBy(int delta)
    {
        int before = _value;
        Value = _value + delta;
        if (_value == before) return false;
        Scrolled?.Invoke(_value);
        return true;
    }

    private void Track()
    {
        if (_value > MaxValue) _value = MaxValue;
    }

    private int MaxValue => Math.Max(0, _maximum - _viewport);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(BackColor)) g.FillRectangle(bg, ClientRectangle);
        if (!Scrolling) return;

        int inset = 1;
        var thumb = new Rectangle(inset, ThumbTop, Width - inset * 2, ThumbHeight);
        int radius = Math.Max(2, thumb.Width / 2);

        int alpha = _dragging ? 190 : _hover ? 150 : 95;
        using var brush = new SolidBrush(Color.FromArgb(alpha, 176, 182, 190));
        using var path = Rounded(thumb, radius);
        g.FillPath(brush, path);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Scrolling) return;

        var thumb = new Rectangle(0, ThumbTop, Width, ThumbHeight);
        if (thumb.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = e.Y - ThumbTop;
            Capture = true;
        }
        else
        {
            // 点在空白处：朝点击方向翻一屏
            int page = Math.Max(24, _viewport - 40);
            if (ScrollBy(e.Y < ThumbTop ? -page : page)) Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || !Scrolling) return;

        int span = Height - ThumbHeight;
        if (span <= 0) return;

        int top = Math.Clamp(e.Y - _dragOffset, 0, span);
        int target = (int)((long)top * MaxValue / span);
        if (target != _value)
        {
            _value = target;
            Invalidate();
            Scrolled?.Invoke(_value);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Capture = false;
        Invalidate();
    }
}

internal sealed class LevelBar : Control
{
    private int _level;
    private Color _color = Color.FromArgb(79, 140, 255);

    public LevelBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 6;
    }

    public int Level
    {
        get => _level;
        set
        {
            _level = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new Rectangle(0, 0, Width, Height);
        using (var path = Rounded(track, Height / 2))
        using (var brush = new SolidBrush(Color.FromArgb(46, 49, 55)))
            g.FillPath(brush, path);

        int w = (int)Math.Round(Width * (_level / 100.0));
        if (w > 0)
        {
            var fill = new Rectangle(0, 0, Math.Max(w, Height), Height);
            using var path = Rounded(fill, Height / 2);
            using var brush = new SolidBrush(_color);
            g.FillPath(brush, path);
        }
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
