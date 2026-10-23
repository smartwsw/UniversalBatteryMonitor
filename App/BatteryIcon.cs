using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace BatteryMonitor.App;

/// <summary>
/// 托盘图标：安卓系统那种圆环电量条，中间是百分比数字，全部运行时用 GDI+ 现画。
/// </summary>
internal static class BatteryIcon
{
    private const int Size = 32;
    private const float RingRadius = 11.5f;
    private const float RingThickness = 4f;

    private static readonly Color TrackColor = Color.FromArgb(110, 118, 128);
    private static readonly Color OutlineColor = Color.FromArgb(190, 12, 13, 16);

    public static Color LevelColor(int? level) => level switch
    {
        null => Color.FromArgb(150, 155, 160),
        >= 60 => Color.FromArgb(93, 220, 138),
        >= 30 => Color.FromArgb(245, 196, 81),
        _ => Color.FromArgb(255, 107, 107),
    };

    /// <summary>圆环样式：<paramref name="level"/> 为 null 时只画灰色空环。</summary>
    public static Icon Create(int? level)
    {
        var bmp = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var ring = new RectangleF(
                Size / 2f - RingRadius, Size / 2f - RingRadius,
                RingRadius * 2, RingRadius * 2);

            // 先描一圈深色，浅色任务栏上也能看清边界
            using (var outline = new Pen(OutlineColor, RingThickness + 2f))
                g.DrawEllipse(outline, ring);

            using (var track = new Pen(TrackColor, RingThickness))
                g.DrawEllipse(track, ring);

            if (level is int value and > 0)
            {
                using var fill = new Pen(LevelColor(level), RingThickness)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                // 从 12 点方向顺时针画到对应比例；100% 时收小半度，避免首尾端点重叠发亮
                float sweep = Math.Min(359.5f, value * 3.6f);
                g.DrawArc(fill, ring, -90f, sweep);
            }

            DrawLevelText(g, level);
        }

        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        NativeIcon.DestroyIcon(hIcon);
        bmp.Dispose();
        return icon;
    }

    private static void DrawLevelText(Graphics g, int? level)
    {
        string text = level?.ToString() ?? "--";
        float fontSize = text.Length >= 3 ? 10.5f : text.Length == 2 ? 12f : 13.5f;

        using var path = new GraphicsPath();
        using var family = new FontFamily("Segoe UI");
        path.AddString(text, family, (int)FontStyle.Bold, fontSize,
            new PointF(Size / 2f, Size / 2f + 0.5f), new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            });

        using (var outline = new Pen(OutlineColor, 1.6f))
            g.DrawPath(outline, path);

        using var fill = new SolidBrush(level is null ? TrackColor : Color.FromArgb(240, 244, 248));
        g.FillPath(fill, path);
    }
}

internal static class NativeIcon
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}
