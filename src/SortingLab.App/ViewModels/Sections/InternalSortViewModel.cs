using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortingLab.App.Services;
using SortingLab.App.ViewModels.Common;
using SortingLab.App.Visualization;
using SortingLab.Core.Abstractions;
using SortingLab.Core.Algorithms.Internal;
using SortingLab.Core.Execution;
using SortingLab.Core.Models;

namespace SortingLab.App.ViewModels.Sections;

public sealed partial class InternalSortViewModel : ObservableObject
{
    private readonly Random _random = new();
    private int[] _input = Array.Empty<int>();

    [ObservableProperty]
    private AlgorithmChoice? _selectedAlgorithm;

    [ObservableProperty]
    private decimal _arraySize = 16;

    [ObservableProperty]
    private decimal _minValue = 5;

    [ObservableProperty]
    private decimal _maxValue = 120;

    [ObservableProperty]
    private string _manualInput = string.Empty;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private int _visualRevision;

    [ObservableProperty]
    private bool _isTournament;

    [ObservableProperty]
    private bool _isGnome;

    [ObservableProperty]
    private int _selectedVisualizationTab;

    [ObservableProperty]
    private string _visualStateText = string.Empty;

    [ObservableProperty]
    private bool _isVisualStateVisible;

    [ObservableProperty]
    private TournamentSnapshot? _tournament;

    [ObservableProperty]
    private string _originalArrayText = string.Empty;

    [ObservableProperty]
    private string _sortedArrayText = string.Empty;

    [ObservableProperty]
    private string _arraySummary = "Массив не задан";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isInputEnabled = true;

    public InternalSortViewModel()
    {
        var oddEven = new OddEvenSort();
        var tournament = new TournamentSort();
        var gnome = new GnomeSort();
        Algorithms = new ObservableCollection<AlgorithmChoice>
        {
            new(oddEven, oddEven.Descriptor),
            new(tournament, tournament.Descriptor),
            new(gnome, gnome.Descriptor)
        };

        _selectedAlgorithm = Algorithms[0];
        Playback = new PlaybackViewModel();
        Playback.PropertyChanged += OnPlaybackPropertyChanged;
        Playback.Configure(
            CreateStream,
            ApplyStep,
            ResetVisuals,
            ResetVisuals,
            ValidateStart);

        var example = new[] { 42, 17, 8, 91, 23, 56, 4, 78, 33, 12, 67, 29, 50, 11, 84, 39 };
        SetInput(example);
    }

    public ObservableCollection<AlgorithmChoice> Algorithms { get; }

    public PlaybackViewModel Playback { get; }

    public List<BarDatum> Bars { get; } = new();

    public List<BarDatum> ResultBars { get; } = new();

    public ObservableCollection<ArrayCellViewModel> Cells { get; } = new();

    public ObservableCollection<ArrayCellViewModel> ResultCells { get; } = new();

    public AlgorithmDescriptor? AlgorithmInfo => SelectedAlgorithm?.Descriptor;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public string InputHint => IsTournament
        ? "Tournament Sort переносит минимумы в отдельный результирующий массив."
        : IsGnome
            ? "Gnome Sort идёт по массиву и возвращается назад, пока соседние элементы стоят неправильно."
            : "Odd-Even Sort переставляет соседние элементы, пока массив не станет упорядоченным.";

    [RelayCommand]
    private void Generate()
    {
        if (ArraySize < 0 || ArraySize > 200)
        {
            ValidationMessage = "Размер массива должен быть от 0 до 200.";
            return;
        }

        if (MinValue > MaxValue)
        {
            ValidationMessage = "Минимальное значение не должно быть больше максимального.";
            return;
        }

        var size = (int)ArraySize;
        var min = (int)MinValue;
        var max = (int)MaxValue;
        var values = new int[size];
        for (var i = 0; i < size; i++)
        {
            values[i] = _random.Next(min, max + 1);
        }

        ValidationMessage = null;
        SetInput(values);
    }

    [RelayCommand]
    private void ApplyManual()
    {
        var text = ManualInput.Trim();
        if (text.Length == 0)
        {
            SetInput(Array.Empty<int>());
            ValidationMessage = null;
            return;
        }

        var separators = new[] { ',', ';', ' ', '\r', '\n', '\t' };
        var parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        var values = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                ValidationMessage = $"Некорректное число: «{part}». Вводите целые числа через пробел или запятую.";
                return;
            }

            values.Add(value);
        }

        if (values.Count > 200)
        {
            ValidationMessage = "Для демонстрации можно ввести не более 200 чисел.";
            return;
        }

        ValidationMessage = null;
        SetInput(values.ToArray());
    }

    [RelayCommand]
    private void Clear()
    {
        ManualInput = string.Empty;
        ValidationMessage = null;
        SetInput(Array.Empty<int>());
    }

    private void SetInput(int[] values)
    {
        if (Playback.State != DemoPlaybackState.Idle)
        {
            Playback.ResetCommand.Execute(null);
        }

        _input = values.ToArray();
        if (ArraySize != _input.Length && _input.Length <= 200)
        {
            ArraySize = _input.Length;
        }

        ResetVisuals();
    }

    private void ResetVisuals()
    {
        Bars.Clear();
        Cells.Clear();
        ResultBars.Clear();
        ResultCells.Clear();
        Tournament = null;
        HasResult = false;
        SortedArrayText = string.Empty;
        VisualStateText = string.Empty;
        IsVisualStateVisible = false;

        for (var index = 0; index < _input.Length; index++)
        {
            Bars.Add(new BarDatum(_input[index], BarState.Normal, index));
            Cells.Add(new ArrayCellViewModel(index, _input[index]));
        }

        OriginalArrayText = FormatArray(_input);
        ArraySummary = _input.Length == 0
            ? "Массив пуст"
            : $"Элементов: {_input.Length}   ·   минимум: {_input.Min()}   ·   максимум: {_input.Max()}";
        VisualRevision++;
    }

    private void ApplyStep(DemoStep baseStep)
    {
        if (baseStep is not ArraySortStep step)
        {
            return;
        }

        switch (step.Kind)
        {
            case ArrayStepKind.Initial when step.Snapshot is not null:
                _input = (int[])step.Snapshot.Clone();
                ResetVisuals();
                break;

            case ArrayStepKind.Position:
                ClearHighlights();
                if (step.PositionIndex is { } position)
                {
                    VisualStateText = position < 0 ? "Начало массива" : $"Позиция: {position}";
                    IsVisualStateVisible = true;
                    if (position >= 0 && position < Bars.Count)
                    {
                        SetBarState(position, BarState.Active);
                        SetCellState(position, RowHighlight.Active);
                    }
                }

                break;

            case ArrayStepKind.Compare:
                ClearHighlights();
                foreach (var index in step.Indices)
                {
                    SetBarState(index, BarState.Comparing);
                    SetCellState(index, RowHighlight.Comparing);
                }

                break;

            case ArrayStepKind.Swap:
                ApplySwap(step.Indices, step.Values);
                ClearHighlights();
                foreach (var index in step.Indices)
                {
                    SetBarState(index, BarState.Active);
                    SetCellState(index, RowHighlight.Active);
                }

                break;

            case ArrayStepKind.Select:
                ClearHighlights();
                if (step.Tournament is not null)
                {
                    Tournament = step.Tournament;
                    ApplyTreeStates(step.Tournament);
                }

                foreach (var index in step.Indices)
                {
                    SetBarState(index, BarState.Active);
                    SetCellState(index, RowHighlight.Active);
                }

                break;

            case ArrayStepKind.Write:
                ClearHighlights();
                if (step.ResultIndex is not null && step.ResultValue is not null)
                {
                    WriteResult(step.ResultIndex.Value, step.ResultValue.Value);
                    foreach (var index in step.Indices)
                    {
                        SetBarState(index, BarState.Excluded);
                        SetCellState(index, RowHighlight.Excluded);
                    }
                }

                HasResult = IsTournament;
                break;

            case ArrayStepKind.TreeUpdated when step.Tournament is not null:
                Tournament = step.Tournament;
                ClearHighlights();
                ApplyTreeStates(step.Tournament);
                break;

            case ArrayStepKind.Phase:
            case ArrayStepKind.PassCompleted:
                ClearHighlights();
                break;

            case ArrayStepKind.Completed when step.Snapshot is not null:
                SortedArrayText = FormatArray(step.Snapshot);
                if (IsGnome)
                {
                    VisualStateText = "Массив отсортирован";
                    IsVisualStateVisible = true;
                }
                if (IsTournament)
                {
                    ResultBars.Clear();
                    ResultCells.Clear();
                    for (var index = 0; index < step.Snapshot.Length; index++)
                    {
                        ResultBars.Add(new BarDatum(step.Snapshot[index], BarState.Result, index));
                        ResultCells.Add(new ArrayCellViewModel(index, step.Snapshot[index], RowHighlight.Sorted));
                    }

                    HasResult = true;
                    foreach (var cell in Cells)
                    {
                        cell.State = cell.State == RowHighlight.Excluded ? RowHighlight.Excluded : RowHighlight.Sorted;
                    }
                }
                else
                {
                    Bars.Clear();
                    Cells.Clear();
                    for (var index = 0; index < step.Snapshot.Length; index++)
                    {
                        Bars.Add(new BarDatum(step.Snapshot[index], BarState.Sorted, index));
                        Cells.Add(new ArrayCellViewModel(index, step.Snapshot[index], RowHighlight.Sorted));
                    }
                }

                break;
        }

        VisualRevision++;
    }

    private void ApplySwap(IReadOnlyList<int> indices, IReadOnlyList<int> oldValues)
    {
        if (indices.Count < 2 || indices[0] < 0 || indices[1] >= Bars.Count)
        {
            return;
        }

        var left = indices[0];
        var right = indices[1];
        if (left >= Bars.Count || right >= Bars.Count)
        {
            return;
        }

        var leftValue = Bars[left].Value;
        var rightValue = Bars[right].Value;
        Bars[left] = Bars[left] with { Value = rightValue };
        Bars[right] = Bars[right] with { Value = leftValue };

        if (left < Cells.Count && right < Cells.Count)
        {
            (Cells[left].Value, Cells[right].Value) = (Cells[right].Value, Cells[left].Value);
        }
    }

    private void WriteResult(int resultIndex, int value)
    {
        while (ResultBars.Count <= resultIndex)
        {
            ResultBars.Add(new BarDatum(0, BarState.Result, ResultBars.Count));
            ResultCells.Add(new ArrayCellViewModel(ResultCells.Count, 0, RowHighlight.Written));
        }

        ResultBars[resultIndex] = new BarDatum(value, BarState.Result, resultIndex);
        ResultCells[resultIndex].Value = value;
        ResultCells[resultIndex].State = RowHighlight.Written;
    }

    private void ApplyTreeStates(TournamentSnapshot snapshot)
    {
        var excluded = new HashSet<int>();
        for (var leaf = 0; leaf < snapshot.ElementCount; leaf++)
        {
            var node = snapshot.Nodes[snapshot.LeafCount + leaf];
            if (node.WinnerIndex is null)
            {
                excluded.Add(leaf);
            }
        }

        for (var index = 0; index < Bars.Count; index++)
        {
            Bars[index] = Bars[index] with
            {
                State = excluded.Contains(index) ? BarState.Excluded : BarState.Normal
            };
            if (index < Cells.Count)
            {
                Cells[index].State = excluded.Contains(index) ? RowHighlight.Excluded : RowHighlight.None;
            }
        }

        if (snapshot.WinnerIndex is { } winner && winner < Bars.Count)
        {
            Bars[winner] = Bars[winner] with { State = BarState.Active };
            if (winner < Cells.Count)
            {
                Cells[winner].State = RowHighlight.Active;
            }
        }
    }

    private void ClearHighlights()
    {
        for (var index = 0; index < Bars.Count; index++)
        {
            if (Bars[index].State is BarState.Comparing or BarState.Active)
            {
                Bars[index] = Bars[index] with { State = BarState.Normal };
            }

            if (index < Cells.Count && Cells[index].State is RowHighlight.Comparing or RowHighlight.Active or RowHighlight.Selected)
            {
                Cells[index].State = RowHighlight.None;
            }
        }
    }

    private void SetBarState(int index, BarState state)
    {
        if (index >= 0 && index < Bars.Count)
        {
            Bars[index] = Bars[index] with { State = state };
        }
    }

    private void SetCellState(int index, RowHighlight state)
    {
        if (index >= 0 && index < Cells.Count)
        {
            Cells[index].State = state;
        }
    }

    private DemonstrationStream CreateStream(CancellationToken cancellationToken)
    {
        var algorithm = SelectedAlgorithm?.Algorithm ?? new OddEvenSort();
        var input = _input.ToArray();
        var channel = new StepChannel<ArraySortStep>();
        var completion = Task.Run(async () =>
        {
            try
            {
                await algorithm.SortAsync(input, channel, cancellationToken);
                channel.Complete();
            }
            catch (OperationCanceledException)
            {
                channel.Complete();
            }
            catch (Exception exception)
            {
                channel.Complete(exception);
            }
        }, cancellationToken);

        return new DemonstrationStream(channel, completion);
    }

    private string? ValidateStart()
    {
        if (SelectedAlgorithm is null)
        {
            return "Выберите алгоритм сортировки.";
        }

        if (_input.Length > 200)
        {
            return "Для визуализации допустимо не более 200 элементов.";
        }

        return null;
    }

    private void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlaybackViewModel.IsActive) or nameof(PlaybackViewModel.State))
        {
            IsInputEnabled = !Playback.IsActive;
        }
    }

    partial void OnSelectedAlgorithmChanged(AlgorithmChoice? value)
    {
        IsTournament = value?.Descriptor.Id == "tournament";
        IsGnome = value?.Descriptor.Id == "gnome";
        SelectedVisualizationTab = 0;
        OnPropertyChanged(nameof(AlgorithmInfo));
        OnPropertyChanged(nameof(InputHint));
        if (Playback.State != DemoPlaybackState.Idle)
        {
            Playback.ResetCommand.Execute(null);
        }
        else
        {
            ResetVisuals();
        }
    }

    partial void OnValidationMessageChanged(string? value) =>
        OnPropertyChanged(nameof(HasValidationError));

    private static string FormatArray(IReadOnlyList<int> values) =>
        values.Count == 0 ? "[]" : $"[{string.Join(", ", values)}]";
}
