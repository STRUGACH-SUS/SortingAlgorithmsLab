using SortingLab.Core.Abstractions;
using SortingLab.Core.Execution;
using SortingLab.Core.Models;

namespace SortingLab.Core.Algorithms.Internal;

/// <summary>
/// Рекурсивная сортировка слиянием. Массив делится на две части,
/// затем отсортированные части сливаются через буфер.
/// </summary>
public sealed class MergeSort : IArraySortAlgorithm
{
    private readonly List<RangeNode> _ranges = new();
    private int[] _buffer = Array.Empty<int>();
    private int[] _leftBuffer = Array.Empty<int>();
    private int[] _rightBuffer = Array.Empty<int>();

    public AlgorithmDescriptor Descriptor { get; } = new(
        Id: "merge",
        Name: "Merge Sort",
        Category: "Усовершенствованный алгоритм",
        ShortDescription: "Сортировка слиянием: массив рекурсивно делится на части, а затем отсортированные части объединяются.",
        Idea: "Сортировка слиянием (Merge Sort) использует принцип «разделяй и властвуй»: сначала массив делится на маленькие части, затем они попарно сливаются в упорядоченные участки.",
        Principle: "Рекурсивно делим диапазон пополам, пока не получим отдельные элементы. При слиянии сравниваем первые доступные элементы двух частей, выбираем меньший и записываем его в буфер. После слияния буфер возвращается в массив.",
        TimeComplexity: "O(n log n) в лучшем, среднем и худшем случаях",
        SpaceComplexity: "O(n) для буфера слияния",
        Features: "Устойчивая сортировка при выборе левого элемента при равенстве. Хорошо предсказуема по времени, но требует дополнительной памяти.");

    public async Task<ArraySortResult> SortAsync(
        IReadOnlyList<int> values,
        IStepSink<ArraySortStep> stepSink,
        CancellationToken cancellationToken = default)
    {
        var array = values.ToArray();
        var context = new ArraySortContext(array, stepSink);
        await context.InitialAsync(cancellationToken);

        if (array.Length == 0)
        {
            await context.CompleteAsync(
                array,
                "Массив пуст — сортировать нечего.",
                cancellationToken);
            return new ArraySortResult((int[])array.Clone(), context.Statistics);
        }

        _ranges.Clear();
        _buffer = new int[array.Length];
        _leftBuffer = new int[array.Length];
        _rightBuffer = new int[array.Length];

        var root = BuildRange(0, array.Length - 1, 0);
        await SortRangeAsync(root, array, context, cancellationToken);

        await context.CompleteAsync(
            array,
            "Все части объединены. Получен единый отсортированный массив.",
            cancellationToken);

        return new ArraySortResult((int[])array.Clone(), context.Statistics);
    }

    private async Task SortRangeAsync(
        RangeNode node,
        int[] array,
        ArraySortContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.Start == node.End)
        {
            node.State = MergeRangeState.Sorted;
            await context.MergeLeafAsync(
                node.Start,
                Snapshot(array.Length),
                $"В диапазоне [{node.Start}–{node.End}] остался один элемент, поэтому он уже считается отсортированным.",
                cancellationToken);
            return;
        }

        var mid = (node.Start + node.End) / 2;
        node.State = MergeRangeState.Splitting;
        await context.SplitAsync(
            node.Start,
            mid,
            node.End,
            Snapshot(array.Length),
            $"Разделяем диапазон [{node.Start}–{node.End}] на две части: [{node.Start}–{mid}] и [{mid + 1}–{node.End}].",
            cancellationToken);

        await SortRangeAsync(node.Left!, array, context, cancellationToken);
        await SortRangeAsync(node.Right!, array, context, cancellationToken);

        node.State = MergeRangeState.Merging;
        await context.MergeStartAsync(
            node.Start,
            mid,
            node.End,
            Snapshot(array.Length),
            $"Начинаем объединять две отсортированные части: [{node.Start}–{mid}] и [{mid + 1}–{node.End}].",
            cancellationToken);

        await MergeRangesAsync(node.Start, mid, node.End, array, context, cancellationToken);

        node.State = MergeRangeState.Sorted;
        await context.MergeCompleteAsync(
            node.Start,
            mid,
            node.End,
            Snapshot(array.Length),
            $"Слияние диапазона [{node.Start}–{node.End}] завершено. Получен отсортированный участок.",
            cancellationToken);
    }

    private async Task MergeRangesAsync(
        int start,
        int mid,
        int end,
        int[] array,
        ArraySortContext context,
        CancellationToken cancellationToken)
    {
        var leftCount = mid - start + 1;
        var rightCount = end - mid;
        Array.Copy(array, start, _leftBuffer, 0, leftCount);
        Array.Copy(array, mid + 1, _rightBuffer, 0, rightCount);

        var left = 0;
        var right = 0;
        var output = 0;

        while (left < leftCount && right < rightCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var leftIndex = start + left;
            var rightIndex = mid + 1 + right;
            var leftValue = _leftBuffer[left];
            var rightValue = _rightBuffer[right];
            var chooseLeft = leftValue <= rightValue;

            await context.CompareAsync(
                leftIndex,
                rightIndex,
                chooseLeft
                    ? $"Сравниваем первые доступные элементы: {leftValue} и {rightValue}. Левое число не больше правого, поэтому выбираем его."
                    : $"Сравниваем первые доступные элементы: {leftValue} и {rightValue}. Правое число меньше, поэтому выбираем его.",
                cancellationToken);

            var chosenIndex = chooseLeft ? leftIndex : rightIndex;
            var chosenValue = chooseLeft ? leftValue : rightValue;
            await context.MergeSelectAsync(
                chosenIndex,
                chosenValue,
                $"Выбираем число {chosenValue} и переносим его в буфер на позицию {output}.",
                cancellationToken);

            _buffer[output] = chosenValue;
            await context.MergeWriteAsync(
                output,
                chosenIndex,
                chosenValue,
                $"Записываем {chosenValue} в буфер слияния на позицию {output}.",
                cancellationToken);

            if (chooseLeft)
            {
                left++;
            }
            else
            {
                right++;
            }

            output++;
        }

        while (left < leftCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceIndex = start + left;
            var value = _leftBuffer[left];
            await WriteRemainingAsync(sourceIndex, value, output, context, cancellationToken);
            left++;
            output++;
        }

        while (right < rightCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceIndex = mid + 1 + right;
            var value = _rightBuffer[right];
            await WriteRemainingAsync(sourceIndex, value, output, context, cancellationToken);
            right++;
            output++;
        }

        for (var index = 0; index < output; index++)
        {
            var value = _buffer[index];
            var targetIndex = start + index;
            array[targetIndex] = value;
            await context.MergeCopyBackAsync(
                targetIndex,
                value,
                $"Возвращаем число {value} из буфера в массив на позицию {targetIndex}.",
                cancellationToken);
        }
    }

    private async ValueTask WriteRemainingAsync(
        int sourceIndex,
        int value,
        int output,
        ArraySortContext context,
        CancellationToken cancellationToken)
    {
        _buffer[output] = value;
        await context.MergeSelectAsync(
            sourceIndex,
            value,
            $"Одна из частей закончилась: переносим оставшееся число {value} в буфер.",
            cancellationToken);
        await context.MergeWriteAsync(
            output,
            sourceIndex,
            value,
            $"Записываем {value} в буфер слияния на позицию {output}.",
            cancellationToken);
    }

    private RangeNode BuildRange(int start, int end, int depth)
    {
        var node = new RangeNode
        {
            Start = start,
            End = end,
            Depth = depth,
            State = MergeRangeState.Pending
        };
        _ranges.Add(node);

        if (start < end)
        {
            var mid = (start + end) / 2;
            node.Left = BuildRange(start, mid, depth + 1);
            node.Right = BuildRange(mid + 1, end, depth + 1);
        }

        return node;
    }

    private MergeLevelsSnapshot Snapshot(int arrayLength) =>
        new(
            arrayLength,
            _ranges
                .Select(node => new MergeRangeSnapshot(node.Start, node.End, node.Depth, node.State))
                .ToArray());

    private sealed class RangeNode
    {
        public int Start { get; init; }

        public int End { get; init; }

        public int Depth { get; init; }

        public MergeRangeState State { get; set; }

        public RangeNode? Left { get; set; }

        public RangeNode? Right { get; set; }
    }
}
