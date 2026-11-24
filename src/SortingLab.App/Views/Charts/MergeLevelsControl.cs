using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SortingLab.Core.Models;

namespace SortingLab.App.Views.Charts;

/// <summary>
/// Уровни рекурсивного деления и слияния Merge Sort.
/// </summary>
public sealed class MergeLevelsControl : Control
{
    private static readonly IBrush Pending = new SolidColorBrush(Color.Parse("#F1F5F9"));
    private static readonly IBrush Splitting = new SolidColorBrush(Color.Parse("#FDE68A"));
    private static readonly IBrush Merging = new SolidColorBrush(Color.Parse("#BFDBFE"));
    private static readonly IBrush Sorted = new SolidColorBrush(Color.Parse("#99F6E4"));

    public static readonly StyledProperty<MergeLevelsSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<MergeLevelsControl, MergeLevelsSnapshot?>(nameof(Snapshot));

    static MergeLevelsControl()
    {
        AffectsRender<MergeLevelsControl>(SnapshotProperty);
    }

    public MergeLevelsSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var snapshot = Snapshot;
        if (snapshot is null || snapshot.Ranges.Count == 0 || snapshot.ArrayLength <= 0)
        {
            DrawCenteredText(context, "Уровни появятся после запуска Merge Sort", 12, ChartPalette.TextMuted);
            return;
        }

        const double left = 44;
        const double right = 14;
        const double top = 14;
        const double bottom = 28;
        var plot = new Rect(
            left,
            top,
            Math.Max(1, Bounds.Width - left - right),
            Math.Max(1, Bounds.Height - top - bottom));
        var maxDepth = Math.Max(1, snapshot.MaxDepth);
        var rowHeight = plot.Height / (maxDepth + 1);
        var n = Math.Max(1, snapshot.ArrayLength);

        for (var depth = 0; depth <= maxDepth; depth++)
        {
            var y = plot.Top + depth * rowHeight;
            var label = CreateText(depth.ToString(CultureInfo.InvariantCulture), 9, ChartPalette.TextMuted);
            context.DrawText(label, new Point(10, y + rowHeight / 2 - label.Height / 2));
        }

        foreach (var range in snapshot.Ranges)
        {
            var x = plot.Left + plot.Width * range.Start / n;
            var width = Math.Max(2, plot.Width * range.Length / n - 1);
            var y = plot.Top + range.Depth * rowHeight + rowHeight * 0.2;
            var height = Math.Max(10, rowHeight * 0.6);
            var rect = new Rect(x, y, width, height);
            var brush = range.State switch
            {
                MergeRangeState.Splitting => Splitting,
                MergeRangeState.Merging => Merging,
                MergeRangeState.Sorted => Sorted,
                _ => Pending
            };

            context.DrawRectangle(brush, new Pen(ChartPalette.Border, 0.8), new RoundedRect(rect, 3));

            if (width >= 52 && height >= 13)
            {
                var text = CreateText($"{range.Start}–{range.End}", 8, ChartPalette.TextSecondary);
                if (text.Width < width - 4)
                {
                    context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Center.Y - text.Height / 2));
                }
            }
        }

        var axisPen = new Pen(ChartPalette.Axis, 1);
        context.DrawLine(axisPen, new Point(plot.Left, plot.Bottom + 2), new Point(plot.Right, plot.Bottom + 2));
        DrawAxisLabel(context, "0", plot.Left, plot.Bottom + 8);
        DrawAxisLabel(context, (n - 1).ToString(CultureInfo.InvariantCulture), plot.Right, plot.Bottom + 8);
        if (n > 2)
        {
            DrawAxisLabel(context, (n / 2).ToString(CultureInfo.InvariantCulture), plot.Left + plot.Width / 2, plot.Bottom + 8);
        }
    }

    private static void DrawAxisLabel(DrawingContext context, string value, double x, double y)
    {
        var text = CreateText(value, 9, ChartPalette.TextMuted);
        context.DrawText(text, new Point(x - text.Width / 2, y));
    }

    private void DrawCenteredText(DrawingContext context, string value, double size, IBrush brush)
    {
        var text = CreateText(value, size, brush);
        context.DrawText(
            text,
            new Point(
                Math.Max(0, (Bounds.Width - text.Width) / 2),
                Math.Max(0, (Bounds.Height - text.Height) / 2)));
    }

    private static FormattedText CreateText(
        string text,
        double size,
        IBrush brush,
        FontWeight weight = FontWeight.Normal) =>
        new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyle.Normal, weight),
            size,
            brush);
}
