using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SortingLab.App.ViewModels.Common;

namespace SortingLab.App.Converters;

public sealed class HighlightBrushConverter : IValueConverter
{
    private static readonly IBrush Transparent = Brushes.Transparent;
    private static readonly IBrush Comparing = new SolidColorBrush(Color.Parse("#FFF7ED"));
    private static readonly IBrush Active = new SolidColorBrush(Color.Parse("#EFF6FF"));
    private static readonly IBrush Selected = new SolidColorBrush(Color.Parse("#DBEAFE"));
    private static readonly IBrush Excluded = new SolidColorBrush(Color.Parse("#F1F5F9"));
    private static readonly IBrush Sorted = new SolidColorBrush(Color.Parse("#ECFDF5"));
    private static readonly IBrush Written = new SolidColorBrush(Color.Parse("#F0FDFA"));
    private static readonly IBrush Range = new SolidColorBrush(Color.Parse("#EFF6FF"));
    private static readonly IBrush LeftRange = new SolidColorBrush(Color.Parse("#E0F2FE"));
    private static readonly IBrush RightRange = new SolidColorBrush(Color.Parse("#CCFBF1"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RowHighlight state
            ? state switch
            {
                RowHighlight.Comparing => Comparing,
                RowHighlight.Active => Active,
                RowHighlight.Selected => Selected,
                RowHighlight.Excluded => Excluded,
                RowHighlight.Sorted => Sorted,
                RowHighlight.Written => Written,
                RowHighlight.Range => Range,
                RowHighlight.LeftRange => LeftRange,
                RowHighlight.RightRange => RightRange,
                _ => Transparent
            }
            : Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
