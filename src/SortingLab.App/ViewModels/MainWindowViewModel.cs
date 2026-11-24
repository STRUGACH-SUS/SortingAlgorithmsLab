using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortingLab.App.Services;
using SortingLab.App.ViewModels.Sections;

namespace SortingLab.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private object _currentSection;

    [ObservableProperty]
    private string _sectionTitle = "Внутренняя сортировка";

    [ObservableProperty]
    private string _sectionSubtitle = "Odd-Even, Tournament, Gnome и Merge Sort: пошаговая визуализация, деревья и журнал операций.";

    [ObservableProperty]
    private bool _isInternalSelected = true;

    [ObservableProperty]
    private bool _isExternalSelected;

    [ObservableProperty]
    private bool _isTextSelected;

    public MainWindowViewModel(FileDialogService dialogs)
    {
        InternalSort = new InternalSortViewModel();
        ExternalSort = new ExternalSortViewModel(dialogs);
        TextSort = new TextSortViewModel(dialogs);
        _currentSection = InternalSort;
    }

    public InternalSortViewModel InternalSort { get; }

    public ExternalSortViewModel ExternalSort { get; }

    public TextSortViewModel TextSort { get; }

    public string VersionText => "Практическое задание №4 · C# / Avalonia UI";

    [RelayCommand]
    private void Navigate(string section)
    {
        switch (section)
        {
            case "external":
                CurrentSection = ExternalSort;
                SectionTitle = "Внешняя сортировка";
                SectionSubtitle = "Natural Merge Sort: естественные серии, временные файлы и сохранение результата.";
                IsInternalSelected = false;
                IsExternalSelected = true;
                IsTextSelected = false;
                break;

            case "text":
                CurrentSection = TextSort;
                SectionTitle = "Сортировка текста и сравнение алгоритмов";
                SectionSubtitle = "QuickSort и Radix Sort: частоты слов, замеры времени, таблица и график.";
                IsInternalSelected = false;
                IsExternalSelected = false;
                IsTextSelected = true;
                break;

            default:
                CurrentSection = InternalSort;
                SectionTitle = "Внутренняя сортировка";
                SectionSubtitle = "Odd-Even, Tournament, Gnome и Merge Sort: пошаговая визуализация, деревья и журнал операций.";
                IsInternalSelected = true;
                IsExternalSelected = false;
                IsTextSelected = false;
                break;
        }
    }

    public void Shutdown()
    {
        InternalSort.Playback.Shutdown();
        ExternalSort.Shutdown();
        TextSort.Shutdown();
    }
}
