using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using SortingLab.Core.Abstractions;
using SortingLab.Core.Csv;
using SortingLab.Core.Execution;
using SortingLab.Core.Models;
using SortingLab.Core.Services;

namespace SortingLab.Core.Algorithms.External;

public enum ExternalMergeMode
{
    Natural,
    Direct
}

/// <summary>
/// Внешнее слияние с реальными временными файлами.
/// В режиме Natural ищутся естественные серии, в режиме Direct файл
/// принудительно разбивается на серии по одному элементу.
/// </summary>
public sealed class NaturalMergeSorter : IExternalSortAlgorithm
{
    private const int PreviewSize = 8;
    private readonly ExternalMergeMode _mode;
    private int _runCounter;
    private string _tempDirectory = string.Empty;

    public NaturalMergeSorter(ExternalMergeMode mode = ExternalMergeMode.Natural)
    {
        _mode = mode;
    }

    public AlgorithmDescriptor Descriptor => _mode == ExternalMergeMode.Natural
        ? NaturalDescriptor
        : DirectDescriptor;

    public static AlgorithmDescriptor NaturalDescriptor { get; } = new(
        Id: "natural-merge-external",
        Name: "Natural Merge Sort",
        Category: "Внешняя сортировка",
        ShortDescription: "Естественное слияние: упорядоченные серии находятся в файле и попарно сливаются во временные файлы.",
        Idea: "Файл читается последовательно, из уже упорядоченных участков формируются естественные серии. Затем серии попарно сливаются, пока не останется одна.",
        Principle: "Каждая серия хранится в отдельном временном файле. При слиянии сравниваются текущие записи двух серий; меньшая запись пишется в новый файл. При равенстве выбирается левая запись — сортировка устойчива.",
        TimeComplexity: "O(n log R) сравнений и файлового ввода-вывода, где R — число естественных серий",
        SpaceComplexity: "O(1) записей в буферах слияния; метаданные серий",
        Features: "Устойчива, использует уже существующий порядок, не изменяет исходный CSV, удаляет временные файлы после работы.");

    public static AlgorithmDescriptor DirectDescriptor { get; } = new(
        Id: "external-merge",
        Name: "External Merge Sort",
        Category: "Внешняя сортировка",
        ShortDescription: "Прямое внешнее слияние: файл разбивается на серии по одному элементу, затем серии попарно сливаются.",
        Idea: "Прямое слияние не ищет уже готовые серии. Оно принудительно разбивает файл на минимальные серии, а затем многократно сливает соседние серии во временные файлы.",
        Principle: "Каждая запись сначала образует отдельную серию. Серии попарно сливаются в новые файлы, пока не останется одна упорядоченная серия. При равенстве ключей выбирается левая запись — порядок сохраняется.",
        TimeComplexity: "O(n log n) сравнений и файлового ввода-вывода",
        SpaceComplexity: "O(1) записей в буферах слияния; серии хранятся в файлах",
        Features: "Устойчива, не зависит от исходного порядка, использует реальные временные файлы, удаляет их после работы.");

    public async Task<ExternalSortResult> SortAsync(
        ExternalSortRequest request,
        IStepSink<ExternalSortStep> stepSink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var comparer = new SortKeyComparer(
            request.SortKey.ColumnIndex,
            request.SortKey.Type,
            request.SortKey.Direction);

        var validationErrors = comparer.Validate(request.Records);
        if (validationErrors.Count > 0)
        {
            var details = string.Join(" ", validationErrors.Take(3).Select(error => error.Message));
            throw new CsvFormatException(
                $"В выбранном ключевом столбце есть некорректные значения. {details}" +
                (validationErrors.Count > 3 ? $" И ещё {validationErrors.Count - 3} ошибок." : string.Empty));
        }

        _runCounter = 0;
        _tempDirectory = TempDirectoryManager.CreateSessionDirectory(request.WorkingRoot);
        var context = new ExternalSortContext(stepSink);
        var keyName = request.Headers[request.SortKey.ColumnIndex];
        var directionText = request.SortKey.Direction == SortDirection.Ascending ? "по возрастанию" : "по убыванию";

        try
        {
            await context.EmitAsync(new ExternalSortStep
            {
                Kind = ExternalStepKind.Initial,
                Title = "Исходная таблица",
                Explanation = $"{Descriptor.Name}: записей {request.Records.Count}. Ключевой столбец: «{keyName}», сортировка {directionText}.",
                Runs = Array.Empty<ExternalRunInfo>(),
                PassNumber = 0
            }, cancellationToken);

            if (request.Records.Count == 0)
            {
                context.Stop();
                await context.EmitAsync(new ExternalSortStep
                {
                    Kind = ExternalStepKind.Completed,
                    Title = "Сортировка завершена",
                    Explanation = "Таблица пуста: сортировать нечего.",
                    Runs = Array.Empty<ExternalRunInfo>(),
                    FinalRecordIds = Array.Empty<int>()
                }, cancellationToken);
                return new ExternalSortResult(
                    Array.Empty<CsvRecord>(),
                    context.Statistics,
                    0,
                    0,
                    _tempDirectory);
            }

            var inputPath = Path.Combine(_tempDirectory, "input.jsonl");
            await WriteInputFileAsync(request.Records, inputPath, context, cancellationToken);
            await context.EmitAsync(new ExternalSortStep
            {
                Kind = ExternalStepKind.FileWrite,
                Title = "Рабочий файл создан",
                Explanation = $"Таблица записана во временный файл {Path.GetFileName(inputPath)}. Исходные данные не изменяются.",
                FileName = Path.GetFileName(inputPath)
            }, cancellationToken);

            var runs = _mode == ExternalMergeMode.Natural
                ? await DetectRunsAsync(inputPath, comparer, context, cancellationToken)
                : await DetectDirectRunsAsync(inputPath, comparer, context, cancellationToken);
            var initialRunCount = runs.Count;
            var passCount = 0;

            await context.EmitAsync(new ExternalSortStep
            {
                Kind = ExternalStepKind.RunsCompleted,
                Title = _mode == ExternalMergeMode.Natural
                    ? "Естественные серии определены"
                    : "Серии для прямого слияния созданы",
                Explanation = _mode == ExternalMergeMode.Natural
                    ? $"Найдено серий: {runs.Count}. " +
                      (runs.Count == 1
                          ? "Весь файл уже образует одну упорядоченную серию, слияние не требуется."
                          : "Теперь соседние серии будут попарно сливаться.")
                    : $"Файл разбит на серии по одному элементу: {runs.Count}. " +
                      "Теперь соседние серии будут попарно сливаться.",
                Runs = runs.Select(run => run.ToInfo()).ToArray(),
                PassNumber = 0
            }, cancellationToken);

            if (runs.Count > 1)
            {
                var pass = 1;
                while (runs.Count > 1)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var nextRuns = new List<TempRun>();
                    var outputOffset = 0;

                    for (var index = 0; index < runs.Count; index += 2)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (index + 1 >= runs.Count)
                        {
                            var carried = runs[index];
                            carried.StartIndex = outputOffset;
                            carried.EndIndex = outputOffset + carried.Count - 1;
                            outputOffset += carried.Count;
                            nextRuns.Add(carried);
                            continue;
                        }

                        var merged = await MergePairAsync(
                            runs[index],
                            runs[index + 1],
                            pass,
                            outputOffset,
                            comparer,
                            context,
                            cancellationToken);

                        outputOffset += merged.Count;
                        nextRuns.Add(merged);
                    }

                    runs = nextRuns;
                    passCount = pass;
                    await context.EmitAsync(new ExternalSortStep
                    {
                        Kind = ExternalStepKind.PassCompleted,
                        Title = $"Проход {pass} завершён",
                        Explanation = $"Получено серий: {runs.Count}. " +
                                      (runs.Count == 1
                                          ? "Осталась одна серия — сортировка завершена."
                                          : "Серии снова будут слиты попарно."),
                        Runs = runs.Select(run => run.ToInfo()).ToArray(),
                        PassNumber = pass
                    }, cancellationToken);

                    pass++;
                }
            }

            var sortedRecords = new List<CsvRecord>(request.Records.Count);
            var finalRun = runs[0];
            await foreach (var record in ReadTempRecordsAsync(finalRun.Path, cancellationToken))
            {
                context.CountRead();
                sortedRecords.Add(record);
            }

            if (sortedRecords.Count != request.Records.Count)
            {
                throw new InvalidDataException(
                    $"Внутренняя ошибка внешней сортировки: получено {sortedRecords.Count} записей вместо {request.Records.Count}.");
            }

            context.Stop();
            await context.EmitAsync(new ExternalSortStep
            {
                Kind = ExternalStepKind.Completed,
                Title = "Сортировка завершена",
                Explanation = $"Отсортировано записей: {sortedRecords.Count}. Исходные записи сохранены полностью, включая все столбцы.",
                Runs = runs.Select(run => run.ToInfo()).ToArray(),
                FinalRecordIds = sortedRecords.Select(record => record.Id).ToArray(),
                PassNumber = 0
            }, cancellationToken);

            return new ExternalSortResult(
                sortedRecords,
                context.Statistics,
                initialRunCount,
                passCount,
                _tempDirectory);
        }
        finally
        {
            TempDirectoryManager.TryDelete(_tempDirectory);
        }
    }

    private static void ValidateRequest(ExternalSortRequest request)
    {
        if (request.Headers.Count == 0)
        {
            throw new CsvFormatException("В таблице нет столбцов.");
        }

        if (request.SortKey.ColumnIndex < 0 || request.SortKey.ColumnIndex >= request.Headers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Выбранный ключевой столбец отсутствует в таблице.");
        }

        foreach (var record in request.Records)
        {
            if (record.Fields.Count != request.Headers.Count)
            {
                throw new CsvFormatException(
                    $"Запись #{record.Id} содержит {record.Fields.Count} значений, а столбцов — {request.Headers.Count}.");
            }
        }
    }

    private static async Task WriteInputFileAsync(
        IReadOnlyList<CsvRecord> records,
        string path,
        ExternalSortContext context,
        CancellationToken cancellationToken)
    {
        await using var writer = CreateWriter(path);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteTempRecordAsync(writer, record, cancellationToken);
            context.CountWrite();
        }

        await writer.FlushAsync(cancellationToken);
    }

    private async Task<List<TempRun>> DetectRunsAsync(
        string inputPath,
        SortKeyComparer comparer,
        ExternalSortContext context,
        CancellationToken cancellationToken)
    {
        var runs = new List<TempRun>();
        TempRun? currentRun = null;
        CsvRecord? previous = null;
        var position = 0;

        await context.EmitAsync(new ExternalSortStep
        {
            Kind = ExternalStepKind.Scan,
            Title = "Поиск естественных серий",
            Explanation = "Читаем рабочий файл по порядку и ищем уже упорядоченные участки.",
            Runs = Array.Empty<ExternalRunInfo>(),
            PassNumber = 0
        }, cancellationToken);

        await foreach (var record in ReadTempRecordsAsync(inputPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.CountRead();

            if (currentRun is null)
            {
                currentRun = StartRun(position, record, comparer);
            }
            else if (previous is not null && comparer.Compare(previous, record) > 0)
            {
                await FinishRunAsync(currentRun, runs, context, cancellationToken);
                currentRun = StartRun(position, record, comparer);
            }

            await WriteRunRecordAsync(currentRun!, record, comparer, context, cancellationToken);
            previous = record;
            position++;
        }

        if (currentRun is not null)
        {
            await FinishRunAsync(currentRun, runs, context, cancellationToken);
        }

        return runs;
    }

    private async Task<List<TempRun>> DetectDirectRunsAsync(
        string inputPath,
        SortKeyComparer comparer,
        ExternalSortContext context,
        CancellationToken cancellationToken)
    {
        var runs = new List<TempRun>();
        var position = 0;

        await context.EmitAsync(new ExternalSortStep
        {
            Kind = ExternalStepKind.Scan,
            Title = "Разбиение на серии",
            Explanation = "Прямое слияние не ищет готовый порядок: каждая запись становится отдельной начальной серией.",
            Runs = Array.Empty<ExternalRunInfo>(),
            PassNumber = 0
        }, cancellationToken);

        await foreach (var record in ReadTempRecordsAsync(inputPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.CountRead();

            var run = StartRun(position, record, comparer);
            await WriteRunRecordAsync(run, record, comparer, context, cancellationToken);
            await FinishRunAsync(run, runs, context, cancellationToken);
            position++;
        }

        return runs;
    }

    private TempRun StartRun(int startIndex, CsvRecord firstRecord, SortKeyComparer comparer)
    {
        var number = ++_runCounter;
        var path = Path.Combine(_tempDirectory, $"run-{number:D4}.jsonl");
        var writer = CreateWriter(path);

        return new TempRun
        {
            Number = number,
            Path = path,
            Writer = writer,
            StartIndex = startIndex,
            EndIndex = startIndex,
            Count = 0,
            FirstKey = comparer.FormatKey(firstRecord),
            LastKey = comparer.FormatKey(firstRecord)
        };
    }

    private static async Task WriteRunRecordAsync(
        TempRun run,
        CsvRecord record,
        SortKeyComparer comparer,
        ExternalSortContext context,
        CancellationToken cancellationToken)
    {
        await WriteTempRecordAsync(run.Writer, record, cancellationToken);
        context.CountWrite();
        run.Count++;
        run.EndIndex = run.StartIndex + run.Count - 1;
        run.LastKey = comparer.FormatKey(record);
        if (run.PreviewRecordIds.Count < PreviewSize)
        {
            run.PreviewRecordIds.Add(record.Id);
        }
    }

    private async Task FinishRunAsync(
        TempRun run,
        List<TempRun> runs,
        ExternalSortContext context,
        CancellationToken cancellationToken)
    {
        await run.Writer.FlushAsync(cancellationToken);
        await run.Writer.DisposeAsync();
        runs.Add(run);

        await context.EmitAsync(new ExternalSortStep
        {
            Kind = ExternalStepKind.RunDetected,
            Title = _mode == ExternalMergeMode.Natural
                ? $"Найдена серия #{run.Number}"
                : $"Создана серия #{run.Number}",
            Explanation = _mode == ExternalMergeMode.Natural
                ? $"Серия #{run.Number}: записей {run.Count}, диапазон {run.StartIndex}–{run.EndIndex}, ключи от «{run.FirstKey}» до «{run.LastKey}»."
                : $"Прямое слияние: серия #{run.Number} из одного элемента, ключ «{run.FirstKey}».",
            Runs = runs.Select(item => item.ToInfo()).ToArray(),
            PassNumber = 0
        }, cancellationToken);
    }

    private async Task<TempRun> MergePairAsync(
        TempRun leftRun,
        TempRun rightRun,
        int pass,
        int outputOffset,
        SortKeyComparer comparer,
        ExternalSortContext context,
        CancellationToken cancellationToken)
    {
        var number = ++_runCounter;
        var path = Path.Combine(_tempDirectory, $"run-{number:D4}.jsonl");
        var output = new TempRun
        {
            Number = number,
            Path = path,
            Writer = CreateWriter(path),
            StartIndex = outputOffset,
            EndIndex = outputOffset,
            FirstKey = "—",
            LastKey = "—"
        };

        await context.EmitAsync(new ExternalSortStep
        {
            Kind = ExternalStepKind.MergeStarted,
            Title = $"Слияние серий #{leftRun.Number} и #{rightRun.Number}",
            Explanation = $"Начинаем слияние: слева {leftRun.Count} записей, справа {rightRun.Count} записей. Результат пишется в серию #{number}.",
            LeftRunNumber = leftRun.Number,
            RightRunNumber = rightRun.Number,
            PassNumber = pass
        }, cancellationToken);

        var leftEnumerator = ReadTempRecordsAsync(leftRun.Path, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var rightEnumerator = ReadTempRecordsAsync(rightRun.Path, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var outputIndex = 0;

        try
        {
            var hasLeft = await leftEnumerator.MoveNextAsync();
            if (hasLeft)
            {
                context.CountRead();
            }

            var hasRight = await rightEnumerator.MoveNextAsync();
            if (hasRight)
            {
                context.CountRead();
            }

            while (hasLeft || hasRight)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CsvRecord chosen;
                string explanation;
                bool chooseLeft;

                if (hasLeft && hasRight)
                {
                    var left = leftEnumerator.Current;
                    var right = rightEnumerator.Current;
                    var comparison = comparer.Compare(left, right);
                    context.CountComparison();
                    var leftKey = comparer.FormatKey(left);
                    var rightKey = comparer.FormatKey(right);

                    await context.EmitAsync(new ExternalSortStep
                    {
                        Kind = ExternalStepKind.Compare,
                        Title = "Сравнение записей",
                        Explanation = comparison <= 0
                            ? $"Сравниваем ключи «{leftKey}» и «{rightKey}». Левый ключ не больше правого, поэтому выбираем левую запись."
                            : $"Сравниваем ключи «{leftKey}» и «{rightKey}». Правый ключ меньше, поэтому выбираем правую запись.",
                        LeftRecordId = left.Id,
                        RightRecordId = right.Id,
                        LeftKey = leftKey,
                        RightKey = rightKey,
                        LeftRunNumber = leftRun.Number,
                        RightRunNumber = rightRun.Number,
                        PassNumber = pass
                    }, cancellationToken);

                    chooseLeft = comparison <= 0;
                    chosen = chooseLeft ? left : right;
                    explanation = $"Запись #{chosen.Id} выбрана и записывается в серию #{number}.";
                }
                else if (hasLeft)
                {
                    chosen = leftEnumerator.Current;
                    chooseLeft = true;
                    explanation = "Правая серия закончилась: переносим оставшиеся записи левой серии.";
                }
                else
                {
                    chosen = rightEnumerator.Current;
                    chooseLeft = false;
                    explanation = "Левая серия закончилась: переносим оставшиеся записи правой серии.";
                }

                await context.EmitAsync(new ExternalSortStep
                {
                    Kind = ExternalStepKind.Select,
                    Title = "Выбор записи",
                    Explanation = explanation,
                    WrittenRecordId = chosen.Id,
                    LeftRecordId = hasLeft ? leftEnumerator.Current.Id : null,
                    RightRecordId = hasRight ? rightEnumerator.Current.Id : null,
                    PassNumber = pass
                }, cancellationToken);

                await WriteTempRecordAsync(output.Writer, chosen, cancellationToken);
                context.CountWrite();
                output.Count++;
                output.EndIndex = output.StartIndex + output.Count - 1;
                if (output.Count == 1)
                {
                    output.FirstKey = comparer.FormatKey(chosen);
                }

                output.LastKey = comparer.FormatKey(chosen);
                if (output.PreviewRecordIds.Count < PreviewSize)
                {
                    output.PreviewRecordIds.Add(chosen.Id);
                }

                await context.EmitAsync(new ExternalSortStep
                {
                    Kind = ExternalStepKind.WriteRecord,
                    Title = "Запись в результат",
                    Explanation = $"{explanation} Позиция в новой серии: {outputIndex}.",
                    WrittenRecordId = chosen.Id,
                    OutputIndex = outputIndex,
                    PassNumber = pass
                }, cancellationToken);

                outputIndex++;

                if (chooseLeft && hasLeft)
                {
                    hasLeft = await leftEnumerator.MoveNextAsync();
                    if (hasLeft)
                    {
                        context.CountRead();
                    }
                }
                else if (!chooseLeft && hasRight)
                {
                    hasRight = await rightEnumerator.MoveNextAsync();
                    if (hasRight)
                    {
                        context.CountRead();
                    }
                }
            }

            await output.Writer.FlushAsync(cancellationToken);

            await context.EmitAsync(new ExternalSortStep
            {
                Kind = ExternalStepKind.MergeCompleted,
                Title = $"Слияние серий #{leftRun.Number} и #{rightRun.Number} завершено",
                Explanation = $"Получена серия #{number}: записей {output.Count}, ключи от «{output.FirstKey}» до «{output.LastKey}».",
                Runs = new[] { output.ToInfo() },
                LeftRunNumber = leftRun.Number,
                RightRunNumber = rightRun.Number,
                PassNumber = pass
            }, cancellationToken);

            return output;
        }
        finally
        {
            await leftEnumerator.DisposeAsync();
            await rightEnumerator.DisposeAsync();
            await output.Writer.DisposeAsync();
        }
    }

    private static StreamWriter CreateWriter(string path) =>
        new(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static async ValueTask WriteTempRecordAsync(
        StreamWriter writer,
        CsvRecord record,
        CancellationToken cancellationToken)
    {
        var temp = new TempRecord
        {
            Id = record.Id,
            Fields = record.Fields.ToArray()
        };
        var json = JsonSerializer.Serialize(temp);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }

    private static async IAsyncEnumerable<CsvRecord> ReadTempRecordsAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var temp = JsonSerializer.Deserialize<TempRecord>(line)
                       ?? throw new InvalidDataException($"Не удалось прочитать временную запись из файла {Path.GetFileName(path)}.");
            yield return new CsvRecord(temp.Id, temp.Fields ?? Array.Empty<string>());
        }
    }

    private sealed class TempRun
    {
        public required int Number { get; init; }

        public required string Path { get; init; }

        public required StreamWriter Writer { get; init; }

        public int StartIndex { get; set; }

        public int EndIndex { get; set; }

        public int Count { get; set; }

        public string FirstKey { get; set; } = "—";

        public string LastKey { get; set; } = "—";

        public List<int> PreviewRecordIds { get; } = new();

        public ExternalRunInfo ToInfo() =>
            new(Number, StartIndex, EndIndex, Count, FirstKey, LastKey, PreviewRecordIds.ToArray());
    }

    private sealed class TempRecord
    {
        public int Id { get; set; }

        public string[]? Fields { get; set; }
    }
}
