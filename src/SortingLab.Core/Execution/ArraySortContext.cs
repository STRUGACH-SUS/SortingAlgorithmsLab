using SortingLab.Core.Abstractions;
using SortingLab.Core.Models;

namespace SortingLab.Core.Execution;

/// <summary>
/// Контекст выполнения сортировки массива: хранит текущий массив,
/// счётчики и присваивает шагам последовательные номера.
/// </summary>
public sealed class ArraySortContext
{
    private readonly IStepSink<ArraySortStep> _sink;
    private readonly SortStatistics _statistics = new();

    public ArraySortContext(int[] values, IStepSink<ArraySortStep> sink)
    {
        Values = values;
        _sink = sink;
    }

    public int[] Values { get; }

    public int StepNumber { get; private set; }

    public OperationStatistics Statistics => _statistics.Snapshot();

    public void CountComparison() => _statistics.Comparisons++;

    public ValueTask InitialAsync(CancellationToken cancellationToken = default) =>
        EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Initial,
            Title = "Исходный массив",
            Explanation = Values.Length == 0
                ? "Массив пуст — сортировать нечего."
                : $"Получен массив из {Values.Length} элементов. Начинаем сортировку.",
            Snapshot = (int[])Values.Clone()
        }, cancellationToken);

    public ValueTask PositionAsync(
        int position,
        string explanation,
        CancellationToken cancellationToken = default)
    {
        var hasValue = position >= 0 && position < Values.Length;
        return EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Position,
            Title = "Текущая позиция",
            Explanation = explanation,
            PositionIndex = position,
            Indices = hasValue ? new[] { position } : Array.Empty<int>(),
            Values = hasValue ? new[] { Values[position] } : Array.Empty<int>()
        }, cancellationToken);
    }

    public ValueTask CompareAsync(
        int leftIndex,
        int rightIndex,
        string explanation,
        CancellationToken cancellationToken = default)
    {
        _statistics.Comparisons++;
        return EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Compare,
            Title = "Сравнение элементов",
            Explanation = explanation,
            Indices = new[] { leftIndex, rightIndex },
            Values = new[] { Values[leftIndex], Values[rightIndex] }
        }, cancellationToken);
    }

    public ValueTask SwapAsync(
        int leftIndex,
        int rightIndex,
        string explanation,
        CancellationToken cancellationToken = default)
    {
        var leftValue = Values[leftIndex];
        var rightValue = Values[rightIndex];
        _statistics.Swaps++;
        (Values[leftIndex], Values[rightIndex]) = (rightValue, leftValue);

        return EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Swap,
            Title = "Перестановка элементов",
            Explanation = explanation,
            Indices = new[] { leftIndex, rightIndex },
            Values = new[] { leftValue, rightValue }
        }, cancellationToken);
    }

    public ValueTask SelectAsync(
        int index,
        string explanation,
        CancellationToken cancellationToken = default) =>
        EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Select,
            Title = "Выбран минимальный элемент",
            Explanation = explanation,
            Indices = new[] { index },
            Values = new[] { Values[index] }
        }, cancellationToken);

    public ValueTask SelectTournamentAsync(
        int index,
        TournamentSnapshot tree,
        string explanation,
        CancellationToken cancellationToken = default) =>
        EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Select,
            Title = "Победитель турнира",
            Explanation = explanation,
            Indices = new[] { index },
            Values = new[] { Values[index] },
            Tournament = tree
        }, cancellationToken);

    public ValueTask WriteResultAsync(
        int resultIndex,
        int sourceIndex,
        int value,
        string explanation,
        CancellationToken cancellationToken = default)
    {
        _statistics.Moves++;
        return EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Write,
            Title = "Запись в результат",
            Explanation = explanation,
            Indices = new[] { sourceIndex },
            Values = new[] { value },
            ResultIndex = resultIndex,
            ResultValue = value
        }, cancellationToken);
    }

    public ValueTask PhaseAsync(
        string title,
        string explanation,
        CancellationToken cancellationToken = default) =>
        EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Phase,
            Title = title,
            Explanation = explanation
        }, cancellationToken);

    public ValueTask PassCompletedAsync(
        int pass,
        bool changed,
        string explanation,
        CancellationToken cancellationToken = default) =>
        EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.PassCompleted,
            Title = $"Проход {pass} завершён",
            Explanation = explanation,
            Indices = Array.Empty<int>(),
            Values = Array.Empty<int>()
        }, cancellationToken);

    public ValueTask TreeAsync(
        TournamentSnapshot tree,
        string title,
        string explanation,
        CancellationToken cancellationToken = default) =>
        EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.TreeUpdated,
            Title = title,
            Explanation = explanation,
            Tournament = tree
        }, cancellationToken);

    public async ValueTask CompleteAsync(
        int[] sortedValues,
        string explanation,
        CancellationToken cancellationToken = default)
    {
        _statistics.Stop();
        await EmitAsync(new ArraySortStep
        {
            Kind = ArrayStepKind.Completed,
            Title = "Сортировка завершена",
            Explanation = explanation,
            Snapshot = (int[])sortedValues.Clone()
        }, cancellationToken);
    }

    private async ValueTask EmitAsync(ArraySortStep step, CancellationToken cancellationToken)
    {
        var numbered = step with
        {
            Number = ++StepNumber,
            Statistics = _statistics.Snapshot()
        };
        await _sink.WriteAsync(numbered, cancellationToken);
    }
}
