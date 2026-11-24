using CommunityToolkit.Mvvm.ComponentModel;

namespace SortingLab.App.ViewModels.Common;

public sealed partial class ArrayCellViewModel : ObservableObject
{
    public ArrayCellViewModel(int index, int value, RowHighlight state = RowHighlight.None)
    {
        Index = index;
        _value = value;
        _state = state;
    }

    public int Index { get; }

    [ObservableProperty]
    private int _value;

    [ObservableProperty]
    private RowHighlight _state;

    public string StateText => State switch
    {
        RowHighlight.Comparing => "Сравнивается",
        RowHighlight.Active => "Активен",
        RowHighlight.Selected => "Выбран",
        RowHighlight.Excluded => "Исключён",
        RowHighlight.Sorted => "Готово",
        RowHighlight.Written => "Записан",
        RowHighlight.Range => "Текущий диапазон",
        RowHighlight.LeftRange => "Левая часть",
        RowHighlight.RightRange => "Правая часть",
        _ => "—"
    };

    partial void OnStateChanged(RowHighlight value) =>
        OnPropertyChanged(nameof(StateText));
}
