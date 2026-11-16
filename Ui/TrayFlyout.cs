using System.Drawing.Drawing2D;
using BatteryMonitor.App;

namespace BatteryMonitor.Ui;

/// <summary>
/// 单击托盘图标弹出的小型面板：以列表列出所有外设的名称与电量百分比。
/// 无边框、不占任务栏、失去焦点自动关闭，定位到任务栏所在的那条屏幕边缘。
/// </summary>
internal sealed class TrayFlyout : Form
{
    private const int PanelWidth = 268;
    private const int RowHeight = 38;
    private const int HeaderHeight = 26;
    private const int PadTop = 8;
    private const int PadBottom = 8;
    private const int MaxVisibleRows = 8;

    private static readonly Color Bg = Color.FromArgb(28, 30, 34);
    private static readonly Color Fg = Color.FromArgb(236, 238, 241);
    private static readonly Color Sub = Color.FromArgb(142, 149, 158);
    private static readonly Color Border = Color.FromArgb(58, 63, 72);
    private static readonly Color RowOdd = Color.FromArgb(33, 35, 40);
    private static readonly Color RowEven = Color.FromArgb(38, 41, 47);

    private readonly Label _header;
    private readonly ListHost _list;

    /// <summary>防止刚显示就因为一次意外的失焦事件被立刻关掉。</summary>
    private bool _armed;

    /// <summary>
    /// 延时武装的定时器提成字段：它是局部变量的话，窗体被 Dispose 后仍可能 Tick，
    /// 回调里访问已释放对象会抛异常。
    /// </summary>
    private System.Windows.Forms.Timer? _armTimer;

    public TrayFlyout()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Bg;
        DoubleBuffered = true;
        Font = new Font("Microsoft YaHei UI", 9f);
        ClientSize = new Size(PanelWidth, HeaderHeight + PadTop + PadBottom);

        _header = new Label
        {
            Text = "正在读取…",
            ForeColor = Sub,
            Font = new Font("Microsoft YaHei UI", 8.5f),
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(12, 6),
            Size = new Size(PanelWidth - 24, 16),
        };
        Controls.Add(_header);

        _list = new ListHost
        {
            Location = new Point(0, HeaderHeight),
            Size = new Size(PanelWidth, 0),
        };
        Controls.Add(_list);
    }

    /// <summary>
    /// 用一组设备填充列表。<paramref name="items"/> 为空表示没检测到设备。
    /// 每项为 (名称, 电量, 是否充电, 附加说明)。
    /// </summary>
    public void Update(IReadOnlyList<(string Title, int? Level, bool Charging, string Note)> items)
    {
        _list.SetItems(items);

        int rows = Math.Min(items.Count, MaxVisibleRows);
        int listHeight = rows * RowHeight;
        _list.Size = new Size(PanelWidth, listHeight);

        ClientSize = new Size(PanelWidth, HeaderHeight + listHeight + PadBottom);

        _header.Text = items.Count == 0
            ? "未检测到外设"
            : $"共 {items.Count} 台设备";
    }

    /// <summary>面板可见时用当前数据重新排版，保持一致的高度。</summary>
    public int PreferredHeight(int count)
    {
        int rows = Math.Min(count, MaxVisibleRows);
        return HeaderHeight + rows * RowHeight + PadBottom;
    }

    /// <summary>
    /// 定位到托盘图标旁边。单击托盘图标时光标必定落在任务栏上，
    /// 因此按"光标所在屏幕的任务栏在哪条边"来定方向；
    /// 该屏幕没有任务栏（多显示器常见）时退化为贴着光标上方弹出。
    /// </summary>
    public void ShowNearCursor()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        var area = screen.WorkingArea;
        var bounds = screen.Bounds;

        int w = Width;
        int h = Height;
        const int gap = 8;

        bool taskbarBottom = area.Bottom < bounds.Bottom;
        bool taskbarTop = area.Top > bounds.Top;
        bool taskbarLeft = area.Left > bounds.Left;
        bool taskbarRight = area.Right < bounds.Right;

        int left, top;
        if (taskbarBottom)
        {
            top = area.Bottom - h - gap;
            left = cursor.X - w / 2;
        }
        else if (taskbarTop)
        {
            top = area.Top + gap;
            left = cursor.X - w / 2;
        }
        else if (taskbarLeft)
        {
            left = area.Left + gap;
            top = cursor.Y - h / 2;
        }
        else if (taskbarRight)
        {
            left = area.Right - w - gap;
            top = cursor.Y - h / 2;
        }
        else
        {
            left = cursor.X - w / 2;
            top = cursor.Y - h - gap;
        }

        // 夹到工作区内，避免跑到屏幕外
        left = Math.Max(area.Left + 4, Math.Min(left, area.Right - w - 4));
        top = Math.Max(area.Top + 4, Math.Min(top, area.Bottom - h - 4));

        Location = new Point(left, top);

        _armed = false;
        Show();
        BringToFront();
        Activate();

        StopArmTimer();
        var arm = new System.Windows.Forms.Timer { Interval = 150 };
        _armTimer = arm;
        arm.Tick += (_, _) =>
        {
            StopArmTimer();
            if (IsDisposed) return;
            _armed = true;
        };
        arm.Start();
    }

    private void StopArmTimer()
    {
        var t = _armTimer;
        _armTimer = null;
        if (t is null) return;
        t.Stop();
        t.Dispose();
    }

    public void Toggle()
    {
        if (Visible) Hide();
        else ShowNearCursor();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // 失去焦点即关闭；窗口正在销毁时不要再 Hide，否则会重入
        if (_armed && !IsDisposed && Visible) Hide();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) StopArmTimer();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_TOOLWINDOW：不出现在 Alt+Tab 列表里
            cp.ExStyle |= 0x00000080;
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) StopArmTimer();
        base.Dispose(disposing);
    }

    /// <summary>设备列表的绘制容器：每行名称 + 百分比 + 一条细进度条。</summary>
    private sealed class ListHost : Control
    {
        private readonly List<(string Title, int? Level, bool Charging, string Note)> _items = new();

        public ListHost()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void SetItems(IReadOnlyList<(string Title, int? Level, bool Charging, string Note)> items)
        {
            _items.Clear();
            _items.AddRange(items);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var bg = new SolidBrush(Bg))
                g.FillRectangle(bg, ClientRectangle);

            if (_items.Count == 0)
            {
                using var empty = new SolidBrush(Sub);
                using var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString("未检测到外设", Font, empty, ClientRectangle, fmt);
                return;
            }

            using var titleFont = new Font("Microsoft YaHei UI", 9f);
            using var levelFont = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            using var noteFont = new Font("Microsoft YaHei UI", 7.5f);
            using var titleBrush = new SolidBrush(Fg);
            using var subBrush = new SolidBrush(Sub);

            int visible = Math.Min(_items.Count, MaxVisibleRows);
            for (int i = 0; i < visible; i++)
            {
                var item = _items[i];
                var row = new Rectangle(0, i * RowHeight, Width, RowHeight);

                using (var rowBg = new SolidBrush(i % 2 == 0 ? RowOdd : RowEven))
                    g.FillRectangle(rowBg, row);

                // 名称：失败时用暗色，并且在下方补一行简短说明
                bool failed = item.Level is null;
                int textLeft = 12;
                int textRight = Width - 12;

                // 右侧百分比
                string levelText = item.Level is int lv ? $"{lv}%" : "--";
                Color levelColor = item.Level is null ? Sub : BatteryIcon.LevelColor(item.Level);
                var levelSize = TextRenderer.MeasureText(levelText, levelFont);
                using (var lb = new SolidBrush(levelColor))
                {
                    g.DrawString(levelText, levelFont, lb,
                        textRight - levelSize.Width - 4, row.Top + 7);
                }

                // 名称（避开右上角电量）
                int nameWidth = textRight - textLeft - levelSize.Width - 12;
                var nameRect = new Rectangle(textLeft, row.Top + 4, Math.Max(40, nameWidth), 18);
                using (var nb = new SolidBrush(failed ? Sub : Fg))
                using (var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(item.Title, titleFont, nb, nameRect, nf);

                // 充电标记或简短说明
                string note = item.Charging ? "充电中" : "";
                if (failed && string.IsNullOrEmpty(note)) note = "读取失败";
                if (note.Length > 0)
                {
                    var noteRect = new Rectangle(textLeft, row.Top + 21, Math.Max(40, nameWidth), 14);
                    using var nf2 = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString(note, noteFont, subBrush, noteRect, nf2);
                }

                // 底部细进度条
                int barTop = row.Bottom - 4;
                int barWidth = Width - 24;
                using (var track = new SolidBrush(Color.FromArgb(52, 56, 62)))
                    g.FillRectangle(track, textLeft, barTop, barWidth, 2);

                if (item.Level is int value && value > 0)
                {
                    int w = (int)((long)barWidth * Math.Clamp(value, 0, 100) / 100);
                    using var fill = new SolidBrush(BatteryIcon.LevelColor(item.Level));
                    g.FillRectangle(fill, textLeft, barTop, Math.Max(1, w), 2);
                }
            }

            if (_items.Count > MaxVisibleRows)
            {
                using var more = new SolidBrush(Sub);
                var r = new Rectangle(0, Height - 14, Width - 12, 12);
                using var fmt = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString($"另有 {_items.Count - MaxVisibleRows} 台…", Font, more, r, fmt);
            }
        }
    }
}
