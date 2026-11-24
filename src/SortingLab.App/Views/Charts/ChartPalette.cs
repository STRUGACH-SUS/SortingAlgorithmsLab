using Avalonia.Media;
using SortingLab.App.ViewModels.Common;
using SortingLab.App.Visualization;

namespace SortingLab.App.Views.Charts;

public static class ChartPalette
{
    public static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#CBD5E1"));
    public static readonly IBrush Comparing = new SolidColorBrush(Color.Parse("#F59E0B"));
    public static readonly IBrush Active = new SolidColorBrush(Color.Parse("#2563EB"));
    public static readonly IBrush Sorted = new SolidColorBrush(Color.Parse("#0F766E"));
    public static readonly IBrush Excluded = new SolidColorBrush(Color.Parse("#E2E8F0"));
    public static readonly IBrush Result = new SolidColorBrush(Color.Parse("#0D9488"));
    public static readonly IBrush Grid = new SolidColorBrush(Color.Parse("#E2E8F0"));
    public static readonly IBrush Border = new SolidColorBrush(Color.Parse("#CBD5E1"));
    public static readonly IBrush Axis = new SolidColorBrush(Color.Parse("#94A3B8"));
    public static readonly IBrush TextMuted = new SolidColorBrush(Color.Parse("#64748B"));
    public static readonly IBrush TextSecondary = new SolidColorBrush(Color.Parse("#475569"));
    public static readonly IBrush TextPrimary = new SolidColorBrush(Color.Parse("#0F172A"));
    public static readonly IBrush Quick = new SolidColorBrush(Color.Parse("#2563EB"));
    public static readonly IBrush Radix = new SolidColorBrush(Color.Parse("#0F766E"));
    public static readonly IBrush AccentSoft = new SolidColorBrush(Color.Parse("#EFF6FF"));
    public static readonly IBrush Range = new SolidColorBrush(Color.Parse("#BFDBFE"));
    public static readonly IBrush LeftRange = new SolidColorBrush(Color.Parse("#7DD3FC"));
    public static readonly IBrush RightRange = new SolidColorBrush(Color.Parse("#5EEAD4"));

    public static IBrush ForBar(BarState state) => state switch
    {
        BarState.Comparing => Comparing,
        BarState.Active => Active,
        BarState.Sorted => Sorted,
        BarState.Excluded => Excluded,
        BarState.Result => Result,
        BarState.Range => Range,
        BarState.LeftRange => LeftRange,
        BarState.RightRange => RightRange,
        _ => Normal
    };

    public static IBrush ForHighlight(RowHighlight state) => state switch
    {
        RowHighlight.Comparing => Comparing,
        RowHighlight.Active => Active,
        RowHighlight.Selected => Active,
        RowHighlight.Excluded => Excluded,
        RowHighlight.Sorted => Sorted,
        RowHighlight.Written => Result,
        RowHighlight.Range => Range,
        RowHighlight.LeftRange => LeftRange,
        RowHighlight.RightRange => RightRange,
        _ => Normal
    };
}
