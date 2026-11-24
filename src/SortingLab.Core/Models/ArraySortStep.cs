namespace SortingLab.Core.Models;

public sealed record TournamentNode(
    int NodeIndex,
    int? WinnerIndex,
    int? Value,
    bool IsLeaf,
    bool IsActive);

/// <summary>
/// Снимок турнирного дерева. Индекс 0 не используется, дерево хранится
/// в виде кучи: корень — 1, дети узла v — 2v и 2v+1.
/// </summary>
public sealed record TournamentSnapshot(
    int LeafCount,
    int ElementCount,
    IReadOnlyList<TournamentNode> Nodes,
    int? WinnerIndex,
    IReadOnlyList<int> ActivePath);

public enum ArrayStepKind
{
    Initial,
    Position,
    Compare,
    Swap,
    Select,
    Write,
    Phase,
    PassCompleted,
    TreeUpdated,
    Split,
    MergeLeaf,
    MergeStart,
    MergeSelect,
    MergeWrite,
    MergeCopyBack,
    MergeComplete,
    Completed
}

public sealed record ArraySortStep : DemoStep
{
    public ArrayStepKind Kind { get; init; }

    public IReadOnlyList<int> Indices { get; init; } = Array.Empty<int>();

    public IReadOnlyList<int> Values { get; init; } = Array.Empty<int>();

    public int[]? Snapshot { get; init; }

    public TournamentSnapshot? Tournament { get; init; }

    public int? ResultIndex { get; init; }

    public int? ResultValue { get; init; }

    public int? PositionIndex { get; init; }

    public int? RangeStart { get; init; }

    public int? RangeMid { get; init; }

    public int? RangeEnd { get; init; }

    public MergeLevelsSnapshot? MergeLevels { get; init; }

    public override bool IsInitial => Kind == ArrayStepKind.Initial;

    public override bool IsCompleted => Kind == ArrayStepKind.Completed;

    public override string KindLabel => Kind switch
    {
        ArrayStepKind.Initial => "Исходные данные",
        ArrayStepKind.Position => "Текущая позиция",
        ArrayStepKind.Compare => "Сравнение",
        ArrayStepKind.Swap => "Перестановка",
        ArrayStepKind.Select => "Выбор минимума",
        ArrayStepKind.Write => "Запись результата",
        ArrayStepKind.Phase => "Фаза",
        ArrayStepKind.PassCompleted => "Проход завершён",
        ArrayStepKind.TreeUpdated => "Дерево",
        ArrayStepKind.Split => "Разделение",
        ArrayStepKind.MergeLeaf => "Часть отсортирована",
        ArrayStepKind.MergeStart => "Начало слияния",
        ArrayStepKind.MergeSelect => "Выбор элемента",
        ArrayStepKind.MergeWrite => "Запись в буфер",
        ArrayStepKind.MergeCopyBack => "Возврат в массив",
        ArrayStepKind.MergeComplete => "Слияние завершено",
        ArrayStepKind.Completed => "Сортировка завершена",
        _ => "Операция"
    };

    public override string Details
    {
        get
        {
            if (Kind == ArrayStepKind.Initial)
            {
                return Snapshot is null ? string.Empty : $"Элементов: {Snapshot.Length}";
            }

            if (Kind == ArrayStepKind.Completed)
            {
                return Snapshot is null ? string.Empty : $"Элементов: {Snapshot.Length}";
            }

            if (Kind == ArrayStepKind.Position && PositionIndex is not null)
            {
                return PositionIndex < 0
                    ? "Начало массива"
                    : $"Позиция: {PositionIndex}";
            }

            if (RangeStart is not null && RangeEnd is not null)
            {
                return Kind == ArrayStepKind.MergeStart && RangeMid is not null
                    ? $"Диапазон [{RangeStart}–{RangeMid}] + [{RangeMid + 1}–{RangeEnd}]"
                    : $"Диапазон [{RangeStart}–{RangeEnd}]";
            }

            if (Kind is ArrayStepKind.MergeWrite or ArrayStepKind.MergeCopyBack
                && ResultIndex is not null && ResultValue is not null)
            {
                return Kind == ArrayStepKind.MergeWrite
                    ? $"Буфер[{ResultIndex}] = {ResultValue}"
                    : $"Массив[{ResultIndex}] = {ResultValue}";
            }

            if (Kind == ArrayStepKind.Write && ResultIndex is not null && ResultValue is not null)
            {
                return $"Результат[{ResultIndex}] = {ResultValue}";
            }

            if (Indices.Count == 0)
            {
                return string.Empty;
            }

            var pairs = Indices
                .Select((index, position) => position < Values.Count
                    ? $"[{index}] = {Values[position]}"
                    : $"[{index}]");

            return string.Join(", ", pairs);
        }
    }
}

public sealed record ArraySortResult(int[] SortedValues, OperationStatistics Statistics);
