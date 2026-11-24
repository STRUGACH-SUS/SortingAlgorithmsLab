using SortingLab.Core.Algorithms.Internal;
using SortingLab.Core.Models;

namespace SortingLab.Tests;

public sealed class MergeSortTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { Array.Empty<int>() };
        yield return new object[] { new[] { 5 } };
        yield return new object[] { new[] { 1, 2 } };
        yield return new object[] { new[] { 2, 1 } };
        yield return new object[] { new[] { 1, 2, 3, 4 } };
        yield return new object[] { new[] { 4, 1, 3, 2 } };
        yield return new object[] { new[] { 5, 2, 9, 1, 7 } };
        yield return new object[] { new[] { 5, 4, 3, 2, 1 } };
        yield return new object[] { new[] { 3, 1, 3, 2, 1, 3 } };
        yield return new object[] { new[] { -3, 0, -7, 5, -1, 5, -7 } };
        yield return new object[] { new[] { 0, 0, 0, 0, 0 } };
        yield return new object[] { new[] { 10, -10, 0, 10, -10, 0, 7, 7, 7 } };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MergeSort_SortsCorrectly(int[] input)
    {
        var (result, steps) = await SortTestHelper.RunAsync(new MergeSort(), input);

        SortTestHelper.AssertSorted(result.SortedValues);
        SortTestHelper.AssertSameMultiset(input, result.SortedValues);
        Assert.Equal(input.Length, result.SortedValues.Length);
        Assert.NotEmpty(steps);
        Assert.True(steps[0].IsInitial);
        Assert.True(steps[^1].IsCompleted);
        Assert.Equal(result.SortedValues, steps[^1].Snapshot);
        Assert.Equal(steps.Count, steps[^1].Number);
    }

    [Fact]
    public async Task MergeSort_EmitsSplitMergeAndLevelEvents()
    {
        var input = new[] { 5, 2, 9, 1, 7 };
        var (result, steps) = await SortTestHelper.RunAsync(new MergeSort(), input);

        Assert.True(result.Statistics.Comparisons > 0);
        Assert.True(result.Statistics.Moves > 0);
        Assert.Equal(0, result.Statistics.Swaps);
        Assert.Equal(
            result.Statistics.Comparisons + result.Statistics.Moves,
            result.Statistics.TotalOperations);

        Assert.Contains(steps, step => step.Kind == ArrayStepKind.Split);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.MergeLeaf);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.MergeStart);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.MergeSelect);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.MergeWrite);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.MergeCopyBack);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.MergeComplete);
        Assert.Contains(steps, step => step.MergeLevels is not null);
    }

    [Fact]
    public async Task MergeSort_FirstSplitCoversWholeArray()
    {
        var input = new[] { 4, 1, 3, 2 };
        var (_, steps) = await SortTestHelper.RunAsync(new MergeSort(), input);
        var split = steps.First(step => step.Kind == ArrayStepKind.Split);

        Assert.Equal(0, split.RangeStart);
        Assert.Equal(1, split.RangeMid);
        Assert.Equal(3, split.RangeEnd);
        Assert.NotNull(split.MergeLevels);
        Assert.Equal(7, split.MergeLevels!.Ranges.Count);
        Assert.Equal(2, split.MergeLevels.MaxDepth);
    }

    [Fact]
    public async Task MergeSort_LevelsFinishSorted()
    {
        var input = new[] { 5, 2, 9, 1, 7, 3 };
        var (_, steps) = await SortTestHelper.RunAsync(new MergeSort(), input);
        var levels = steps
            .Where(step => step.MergeLevels is not null)
            .Select(step => step.MergeLevels!)
            .Last();

        Assert.All(levels.Ranges, range => Assert.Equal(MergeRangeState.Sorted, range.State));
        Assert.Equal(0, levels.Ranges.Single(range => range.Depth == 0).Start);
        Assert.Equal(input.Length - 1, levels.Ranges.Single(range => range.Depth == 0).End);
    }

    [Fact]
    public async Task MergeSort_EqualElementsPreferLeftPart()
    {
        var (_, steps) = await SortTestHelper.RunAsync(new MergeSort(), new[] { 1, 1 });

        var selections = steps
            .Where(step => step.Kind == ArrayStepKind.MergeSelect)
            .Select(step => step.Indices[0])
            .ToArray();

        Assert.Equal(new[] { 0, 1 }, selections);
    }

    [Fact]
    public async Task MergeSort_LargeArrayIsSorted()
    {
        var random = new Random(13579);
        var input = Enumerable.Range(0, 200).Select(_ => random.Next(-500, 500)).ToArray();
        var (result, _) = await SortTestHelper.RunAsync(new MergeSort(), input);

        SortTestHelper.AssertSorted(result.SortedValues);
        SortTestHelper.AssertSameMultiset(input, result.SortedValues);
    }

    [Fact]
    public async Task MergeSort_CancellationStopsAlgorithm()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new MergeSort().SortAsync(
                new[] { 3, 2, 1 },
                new Core.Execution.CollectingStepSink<ArraySortStep>(),
                source.Token));
    }
}
