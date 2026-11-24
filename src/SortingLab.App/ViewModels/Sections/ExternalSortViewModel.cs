using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortingLab.App.Services;
using SortingLab.App.ViewModels.Common;
using SortingLab.Core.Abstractions;
using SortingLab.Core.Algorithms.External;
using SortingLab.Core.Csv;
using SortingLab.Core.Execution;
using SortingLab.Core.Models;

namespace SortingLab.App.ViewModels.Sections;

public sealed partial class ExternalSortViewModel : ObservableObject
{
    private const string AllContinents = "Все континенты";
    private readonly FileDialogService _dialogs;
    private readonly Dictionary<int, CsvRecord> _recordsById = new();
    private readonly Dictionary<int, RecordRowViewModel> _rowsById = new();
    private CsvTable? _baseTable;
    private CsvTable? _filteredTable;
    private IReadOnlyList<CsvRecord>? _lastSortedRecords;
    private bool _suppressFilter;

    [ObservableProperty]
    private ExternalAlgorithmChoice? _selectedAlgorithm;

    [ObservableProperty]
    private ColumnOption? _selectedColumn;

    [ObservableProperty]
    private KeyTypeOption? _selectedKeyType;

    [ObservableProperty]
    private DirectionOption? _selectedDirection;

    [ObservableProperty]
    private string _selectedContinent = AllContinents;

    [ObservableProperty]
    private bool _hasContinentColumn;

    [ObservableProperty]
    private string _sourceName = "Встроенная таблица стран";

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _loadMessage = string.Empty;

    [ObservableProperty]
    private string _loadWarnings = string.Empty;

    [ObservableProperty]
    private int _runsRevision;

    [ObservableProperty]
    private RecordRowViewModel? _leftCandidate;

    [ObservableProperty]
    private RecordRowViewModel? _rightCandidate;

    [ObservableProperty]
    private string _leftKey = "—";

    [ObservableProperty]
    private string _rightKey = "—";

    [ObservableProperty]
    private string _currentPass = "Проход 0";

    [ObservableProperty]
    private string _runsSummary = "Серии ещё не найдены";

    [ObservableProperty]
    private string _selectedRecordText = "Сравниваемые записи будут показаны здесь.";

    [ObservableProperty]
    private string? _resultPath;

    [ObservableProperty]
    private string _saveMessage = "После завершения результат можно сохранить в CSV.";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _isInputEnabled = true;

    [ObservableProperty]
    private bool _hasLoadError;

    public ExternalSortViewModel(FileDialogService dialogs)
    {
        _dialogs = dialogs;
        KeyTypes = new ObservableCollection<KeyTypeOption>
        {
            new(SortKeyType.Text, "Строка"),
            new(SortKeyType.Integer, "Целое число"),
            new(SortKeyType.Decimal, "Вещественное число")
        };
        Directions = new ObservableCollection<DirectionOption>
        {
            new(SortDirection.Ascending, "По возрастанию"),
            new(SortDirection.Descending, "По убыванию")
        };
        var natural = new NaturalMergeSorter();
        var external = new ExternalMergeSorter();
        Algorithms = new ObservableCollection<ExternalAlgorithmChoice>
        {
            new(natural, natural.Descriptor),
            new(external, external.Descriptor)
        };
        _selectedAlgorithm = Algorithms[0];
        SelectedKeyType = KeyTypes[0];
        SelectedDirection = Directions[0];

        Playback = new PlaybackViewModel
        {
            StepDelayMilliseconds = 20
        };
        Playback.PropertyChanged += OnPlaybackPropertyChanged;
        Playback.Configure(
            CreateStream,
            ApplyStep,
            ResetVisuals,
            ResetVisuals,
            ValidateStart);

        LoadBuiltIn();
    }

    public ObservableCollection<RecordRowViewModel> SourceRows { get; } = new();

    public ObservableCollection<RecordRowViewModel> ResultRows { get; } = new();

    public ObservableCollection<RunSegmentViewModel> Runs { get; } = new();

    public ObservableCollection<string> Continents { get; } = new() { AllContinents };

    public ObservableCollection<ColumnOption> Columns { get; } = new();

    public ObservableCollection<ExternalAlgorithmChoice> Algorithms { get; }

    public ObservableCollection<KeyTypeOption> KeyTypes { get; }

    public ObservableCollection<DirectionOption> Directions { get; }

    public PlaybackViewModel Playback { get; }

    public AlgorithmDescriptor AlgorithmInfo =>
        SelectedAlgorithm?.Descriptor ?? NaturalMergeSorter.NaturalDescriptor;

    public string AlgorithmHint => SelectedAlgorithm?.Descriptor.Id == "external-merge"
        ? "External Merge Sort принудительно делит файл на серии по одному элементу и сливает их попарно."
        : "Natural Merge Sort находит уже упорядоченные серии и сливает их попарно.";

    public string SeriesTitle => SelectedAlgorithm?.Descriptor.Id == "external-merge"
        ? "Начальные серии"
        : "Естественные серии";

    public IReadOnlyList<string> Headers => _filteredTable?.Headers ?? Array.Empty<string>();

    public string RowCountText => _filteredTable is null
        ? "Таблица не загружена"
        : $"Записей: {_filteredTable.Records.Count:N0}";

    public string FilterInfo => HasContinentColumn
        ? $"Фильтр: {SelectedContinent}"
        : "Фильтр по континенту доступен для таблицы стран";

    public string SortKeyDescription =>
        $"{SelectedColumn?.Name ?? "—"} · {SelectedKeyType?.Name ?? "—"} · {SelectedDirection?.Name ?? "—"}";

    public string ResultTitle => HasResult ? "Отсортированная таблица" : "Результат слияния";

    public bool HasLoadWarnings => !string.IsNullOrWhiteSpace(LoadWarnings);

    public bool HasResultPath => !string.IsNullOrWhiteSpace(ResultPath);

    [RelayCommand]
    private async Task LoadCustomAsync()
    {
        if (Playback.IsActive)
        {
            return;
        }

        try
        {
            var path = await _dialogs.OpenFileAsync("Выберите CSV-файл", _dialogs.CsvType);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var parsed = CsvTableReader.ReadFile(path);
            SetTable(parsed.Table, Path.GetFileName(path), path, parsed.Warnings);
            LoadMessage = $"Загружено записей: {parsed.Table.Records.Count:N0}. Разделитель: «{parsed.Delimiter}».";
            HasLoadError = false;
        }
        catch (Exception exception) when (exception is CsvFormatException or IOException or UnauthorizedAccessException)
        {
            LoadMessage = exception.Message;
            HasLoadError = true;
            LoadWarnings = string.Empty;
        }
        catch (Exception exception)
        {
            LoadMessage = $"Не удалось загрузить CSV: {exception.Message}";
            HasLoadError = true;
        }
    }

    [RelayCommand]
    private void LoadBuiltIn()
    {
        if (Playback.IsActive)
        {
            return;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "countries.csv");
            if (!File.Exists(path))
            {
                LoadMessage = "Встроенный файл Data/countries.csv не найден.";
                HasLoadError = true;
                return;
            }

            var parsed = CsvTableReader.ReadFile(path);
            SetTable(parsed.Table, "Встроенная таблица стран", path, parsed.Warnings);
            LoadMessage = $"Загружено стран: {parsed.Table.Records.Count}.";
            HasLoadError = false;
        }
        catch (Exception exception)
        {
            LoadMessage = $"Не удалось загрузить встроенную таблицу: {exception.Message}";
            HasLoadError = true;
        }
    }

    [RelayCommand]
    private void ResetFilters()
    {
        if (Playback.IsActive)
        {
            return;
        }

        _suppressFilter = true;
        SelectedContinent = AllContinents;
        _suppressFilter = false;
        SelectedColumn = FindDefaultColumn();
        SelectedDirection = Directions[0];
        SelectedKeyType = GuessKeyType(SelectedColumn?.Index ?? 0);
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanSaveResult))]
    private async Task SaveResultAsync()
    {
        if (_lastSortedRecords is null || _filteredTable is null)
        {
            SaveMessage = "Сначала завершите сортировку.";
            return;
        }

        try
        {
            var suggested = $"sorted-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var path = await _dialogs.SaveFileAsync("Сохранить результат", suggested, _dialogs.CsvType);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var table = new CsvTable(_filteredTable.Headers, _lastSortedRecords, SourceName);
            CsvTableWriter.WriteFile(table, path);
            ResultPath = path;
            SaveMessage = $"Результат сохранён: {path}";
            OpenFolderCommand.NotifyCanExecuteChanged();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveMessage = $"Не удалось сохранить файл: {exception.Message}";
        }
        catch (Exception exception)
        {
            SaveMessage = $"Ошибка сохранения: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        _dialogs.OpenFolder(ResultPath ?? SourcePath);
    }

    public void Shutdown()
    {
        Playback.Shutdown();
    }

    private void SetTable(CsvTable table, string sourceName, string sourcePath, IReadOnlyList<string> warnings)
    {
        if (Playback.State != DemoPlaybackState.Idle)
        {
            Playback.ResetCommand.Execute(null);
        }

        _suppressFilter = true;
        _baseTable = table;
        SourceName = sourceName;
        SourcePath = sourcePath;
        LoadWarnings = warnings.Count == 0
            ? string.Empty
            : string.Join(" ", warnings.Take(4)) + (warnings.Count > 4 ? $" …и ещё {warnings.Count - 4}." : string.Empty);

        Columns.Clear();
        for (var index = 0; index < table.Headers.Count; index++)
        {
            Columns.Add(new ColumnOption(index, table.Headers[index]));
        }

        Continents.Clear();
        Continents.Add(AllContinents);
        var continentIndex = FindColumnIndex(table.Headers, "Континент");
        HasContinentColumn = continentIndex >= 0;
        if (continentIndex >= 0)
        {
            foreach (var continent in table.Records
                         .Select(record => record.Field(continentIndex))
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct()
                         .OrderBy(value => value))
            {
                Continents.Add(continent);
            }
        }

        SelectedContinent = AllContinents;
        SelectedColumn = FindDefaultColumn() ?? Columns.FirstOrDefault();
        SelectedKeyType = GuessKeyType(SelectedColumn?.Index ?? 0);
        SelectedDirection = Directions[0];
        _suppressFilter = false;

        ApplyFilter();
        OnPropertyChanged(nameof(HasLoadWarnings));
    }

    private void ApplyFilter()
    {
        if (Playback.State != DemoPlaybackState.Idle)
        {
            Playback.ResetCommand.Execute(null);
        }

        if (_baseTable is null)
        {
            _filteredTable = null;
            SourceRows.Clear();
            _rowsById.Clear();
            _recordsById.Clear();
            OnPropertyChanged(nameof(Headers));
            OnPropertyChanged(nameof(RowCountText));
            return;
        }

        IReadOnlyList<CsvRecord> records = _baseTable.Records;
        var continentIndex = FindColumnIndex(_baseTable.Headers, "Континент");
        if (continentIndex >= 0 && SelectedContinent != AllContinents)
        {
            records = records
                .Where(record => string.Equals(record.Field(continentIndex), SelectedContinent, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        _filteredTable = new CsvTable(_baseTable.Headers, records, SourceName);
        RebuildSourceRows();
        ResultRows.Clear();
        Runs.Clear();
        RunsRevision++;
        RunsSummary = "Серии ещё не найдены";
        CurrentPass = "Проход 0";
        LeftCandidate = null;
        RightCandidate = null;
        LeftKey = "—";
        RightKey = "—";
        SelectedRecordText = "Сравниваемые записи будут показаны здесь.";
        HasResult = false;
        _lastSortedRecords = null;
        ResultPath = null;
        SaveMessage = "После завершения результат можно сохранить в CSV.";
        OnPropertyChanged(nameof(Headers));
        OnPropertyChanged(nameof(RowCountText));
        OnPropertyChanged(nameof(FilterInfo));
        OnPropertyChanged(nameof(ResultTitle));
        SaveResultCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    private void RebuildSourceRows()
    {
        SourceRows.Clear();
        _rowsById.Clear();
        _recordsById.Clear();
        if (_filteredTable is null)
        {
            return;
        }

        foreach (var record in _filteredTable.Records)
        {
            _recordsById[record.Id] = record;
            var row = new RecordRowViewModel(record.Id, record.Fields);
            _rowsById[record.Id] = row;
            SourceRows.Add(row);
        }
    }

    private void ResetVisuals()
    {
        RebuildSourceRows();
        ResultRows.Clear();
        Runs.Clear();
        RunsRevision++;
        RunsSummary = "Серии ещё не найдены";
        CurrentPass = "Проход 0";
        LeftCandidate = null;
        RightCandidate = null;
        LeftKey = "—";
        RightKey = "—";
        SelectedRecordText = "Сравниваемые записи будут показаны здесь.";
        HasResult = false;
        _lastSortedRecords = null;
        ResultPath = null;
        SaveMessage = "После завершения результат можно сохранить в CSV.";
        OnPropertyChanged(nameof(ResultTitle));
        SaveResultCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    private void ApplyStep(DemoStep baseStep)
    {
        if (baseStep is not ExternalSortStep step)
        {
            return;
        }

        switch (step.Kind)
        {
            case ExternalStepKind.Initial:
                RebuildSourceRows();
                ResultRows.Clear();
                CurrentPass = "Подготовка";
                break;

            case ExternalStepKind.Scan:
                CurrentPass = "Поиск естественных серий";
                break;

            case ExternalStepKind.RunDetected:
            case ExternalStepKind.RunsCompleted:
                UpdateRuns(step);
                CurrentPass = step.Kind == ExternalStepKind.RunsCompleted
                    ? "Серии определены"
                    : $"Проход 0 · найдено серий: {Runs.Count}";
                break;

            case ExternalStepKind.MergeStarted:
                ClearSourceHighlights();
                ResultRows.Clear();
                LeftCandidate = null;
                RightCandidate = null;
                LeftKey = "—";
                RightKey = "—";
                CurrentPass = $"Проход {step.PassNumber ?? 0} · слияние серий #{step.LeftRunNumber} и #{step.RightRunNumber}";
                SelectedRecordText = "Начинаем слияние двух серий. Сравниваем текущие записи.";
                break;

            case ExternalStepKind.Compare:
                ClearSourceHighlights();
                LeftCandidate = GetRow(step.LeftRecordId);
                RightCandidate = GetRow(step.RightRecordId);
                LeftKey = step.LeftKey ?? "—";
                RightKey = step.RightKey ?? "—";
                if (LeftCandidate is not null)
                {
                    LeftCandidate.Highlight = RowHighlight.Comparing;
                }

                if (RightCandidate is not null)
                {
                    RightCandidate.Highlight = RowHighlight.Comparing;
                }

                SelectedRecordText = $"Слева ключ {LeftKey}, справа ключ {RightKey}.";
                break;

            case ExternalStepKind.Select:
                if (step.WrittenRecordId is { } selectedId && GetRow(selectedId) is { } selectedRow)
                {
                    selectedRow.Highlight = RowHighlight.Selected;
                    SelectedRecordText = $"Выбрана запись #{selectedId}.";
                }

                break;

            case ExternalStepKind.WriteRecord:
                if (step.WrittenRecordId is { } writtenId && _recordsById.TryGetValue(writtenId, out var written))
                {
                    ResultRows.Add(new RecordRowViewModel(written.Id, written.Fields, RowHighlight.Written));
                    SelectedRecordText = $"Запись #{writtenId} записана в результат.";
                }

                break;

            case ExternalStepKind.MergeCompleted:
                CurrentPass = $"Проход {step.PassNumber ?? 0} · слияние завершено";
                SelectedRecordText = "Серия сформирована. Продолжаем слияние следующей пары.";
                break;

            case ExternalStepKind.PassCompleted:
                UpdateRuns(step);
                CurrentPass = $"Проход {step.PassNumber ?? 0} завершён · серий: {Runs.Count}";
                ClearSourceHighlights();
                LeftCandidate = null;
                RightCandidate = null;
                LeftKey = "—";
                RightKey = "—";
                break;

            case ExternalStepKind.Completed:
                FinishResult(step);
                break;
        }
    }

    private void FinishResult(ExternalSortStep step)
    {
        ResultRows.Clear();
        if (step.FinalRecordIds is not null)
        {
            var sorted = new List<CsvRecord>(step.FinalRecordIds.Count);
            foreach (var id in step.FinalRecordIds)
            {
                if (_recordsById.TryGetValue(id, out var record))
                {
                    sorted.Add(record);
                    ResultRows.Add(new RecordRowViewModel(record.Id, record.Fields, RowHighlight.Sorted));
                }
            }

            _lastSortedRecords = sorted;
        }

        HasResult = true;
        CurrentPass = "Сортировка завершена";
        SelectedRecordText = $"Готово: {ResultRows.Count} записей отсортированы.";
        SaveMessage = "Результат готов. Нажмите «Сохранить CSV», чтобы записать все исходные столбцы.";
        OnPropertyChanged(nameof(ResultTitle));
        SaveResultCommand.NotifyCanExecuteChanged();
    }

    private void UpdateRuns(ExternalSortStep step)
    {
        if (step.Runs is null)
        {
            return;
        }

        Runs.Clear();
        foreach (var run in step.Runs)
        {
            Runs.Add(new RunSegmentViewModel(
                run.Number,
                run.StartIndex,
                run.EndIndex,
                run.Count,
                run.FirstKey,
                run.LastKey));
        }

        RunsRevision++;
        RunsSummary = Runs.Count == 0
            ? "Серии ещё не найдены"
            : $"Серий: {Runs.Count}   ·   записей: {Runs.Sum(run => run.Count)}";
        OnPropertyChanged(nameof(RunsSummary));
    }

    private RecordRowViewModel? GetRow(int? id) =>
        id is { } value && _rowsById.TryGetValue(value, out var row) ? row : null;

    private void ClearSourceHighlights()
    {
        foreach (var row in SourceRows)
        {
            row.Highlight = RowHighlight.None;
        }
    }

    private ColumnOption? FindDefaultColumn()
    {
        if (Columns.Count == 0)
        {
            return null;
        }

        return Columns.FirstOrDefault(column =>
                   column.Name.Contains("Население", StringComparison.OrdinalIgnoreCase) ||
                   column.Name.Contains("Population", StringComparison.OrdinalIgnoreCase))
               ?? Columns[0];
    }

    private KeyTypeOption GuessKeyType(int columnIndex)
    {
        if (_baseTable is null || columnIndex < 0 || columnIndex >= _baseTable.Headers.Count)
        {
            return KeyTypes[0];
        }

        var values = _baseTable.Records
            .Select(record => record.Field(columnIndex))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(30)
            .ToArray();

        if (values.Length == 0)
        {
            return KeyTypes[0];
        }

        var allIntegers = values.All(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
        if (allIntegers)
        {
            return KeyTypes.First(option => option.Value == SortKeyType.Integer);
        }

        var allNumbers = values.All(value =>
        {
            var normalized = value.Replace('\u00A0', ' ').Trim();
            if (normalized.Contains(',') && !normalized.Contains('.'))
            {
                normalized = normalized.Replace(',', '.');
            }

            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        });

        return allNumbers
            ? KeyTypes.First(option => option.Value == SortKeyType.Decimal)
            : KeyTypes[0];
    }

    private static int FindColumnIndex(IReadOnlyList<string> headers, string name)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private DemonstrationStream CreateStream(CancellationToken cancellationToken)
    {
        var table = _filteredTable ?? new CsvTable(Array.Empty<string>(), Array.Empty<CsvRecord>());
        var key = new SortingLab.Core.Csv.SortKey(
            SelectedColumn?.Index ?? 0,
            SelectedKeyType?.Value ?? SortKeyType.Text,
            SelectedDirection?.Value ?? SortDirection.Ascending);
        var channel = new StepChannel<ExternalSortStep>();
        var algorithm = SelectedAlgorithm?.Algorithm ?? new NaturalMergeSorter();
        var completion = Task.Run(async () =>
        {
            try
            {
                _lastSortedRecords = null;
                var result = await algorithm.SortAsync(
                    new ExternalSortRequest(table.Headers, table.Records, key),
                    channel,
                    cancellationToken);
                _lastSortedRecords = result.SortedRecords;
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
        if (_filteredTable is null)
        {
            return "Сначала загрузите таблицу.";
        }

        if (SelectedAlgorithm is null)
        {
            return "Выберите алгоритм внешней сортировки.";
        }

        if (SelectedColumn is null || SelectedColumn.Index < 0 || SelectedColumn.Index >= _filteredTable.Headers.Count)
        {
            return "Выберите ключевой столбец.";
        }

        if (SelectedKeyType is null)
        {
            return "Выберите тип ключевого столбца.";
        }

        var comparer = new SortKeyComparer(SelectedColumn.Index, SelectedKeyType.Value, SelectedDirection?.Value ?? SortDirection.Ascending);
        var errors = comparer.Validate(_filteredTable.Records);
        if (errors.Count > 0)
        {
            var details = string.Join(" ", errors.Take(3).Select(error => error.Message));
            return $"В ключевом столбце есть некорректные значения. {details}";
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

    private bool CanSaveResult() => HasResult && _lastSortedRecords is { Count: > 0 };

    private bool CanOpenFolder() => HasResultPath;

    partial void OnSelectedAlgorithmChanged(ExternalAlgorithmChoice? value)
    {
        OnPropertyChanged(nameof(AlgorithmInfo));
        OnPropertyChanged(nameof(AlgorithmHint));
        OnPropertyChanged(nameof(SeriesTitle));
        if (Playback.State != DemoPlaybackState.Idle)
        {
            Playback.ResetCommand.Execute(null);
        }
        else
        {
            ResetVisuals();
        }
    }

    partial void OnSelectedContinentChanged(string value)
    {
        OnPropertyChanged(nameof(FilterInfo));
        if (!_suppressFilter)
        {
            ApplyFilter();
        }
    }

    partial void OnSelectedColumnChanged(ColumnOption? value)
    {
        OnPropertyChanged(nameof(SortKeyDescription));
        if (!_suppressFilter && value is not null)
        {
            SelectedKeyType = GuessKeyType(value.Index);
        }
    }

    partial void OnSelectedKeyTypeChanged(KeyTypeOption? value) =>
        OnPropertyChanged(nameof(SortKeyDescription));

    partial void OnSelectedDirectionChanged(DirectionOption? value) =>
        OnPropertyChanged(nameof(SortKeyDescription));

    partial void OnHasResultChanged(bool value)
    {
        OnPropertyChanged(nameof(ResultTitle));
        SaveResultCommand.NotifyCanExecuteChanged();
    }

    partial void OnResultPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasResultPath));
        OpenFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnLoadWarningsChanged(string value) =>
        OnPropertyChanged(nameof(HasLoadWarnings));
}
