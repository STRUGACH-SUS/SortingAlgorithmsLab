using SortingLab.Core.Algorithms.Internal;
using SortingLab.Core.Models;

namespace SortingLab.Tests;

public sealed class GnomeSortTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { Array.Empty<int>() };
        yield return new object[] { new[] { 5 } };
        yield return new object[] { new[] { 2, 1 } };
        yield return new object[] { new[] { 1, 2, 3, 4, 5 } };
        yield return new object[] { new[] { 5, 4, 3, 2, 1 } };
        yield return new object[] { new[] { 3, 1, 3, 2, 1, 3 } };
        yield return new object[] { new[] { -3, 0, -7, 5, -1, 5, -7 } };
        yield return new object[] { new[] { 0, 0, 0, 0, 0 } };
        yield return new object[] { new[] { 10, -10, 0, 10, -10, 0, 7, 7, 7 } };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GnomeSort_SortsCorrectly(int[] input)
    {
        var (result, steps) = await SortTestHelper.RunAsync(new GnomeSort(), input);

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
    public async Task GnomeSort_ReverseUsesComparisonsAndSwaps()
    {
        var input = Enumerable.Range(1, 30).Reverse().ToArray();
        var (result, steps) = await SortTestHelper.RunAsync(new GnomeSort(), input);

        Assert.True(result.Statistics.Comparisons > 0);
        Assert.True(result.Statistics.Swaps > 0);
        Assert.Equal(0, result.Statistics.Moves);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.Position);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.Compare);
        Assert.Contains(steps, step => step.Kind == ArrayStepKind.Swap);
    }

    [Fact]
    public async Task GnomeSort_AlreadySortedMakesNoSwaps()
    {
        var input = Enumerable.Range(1, 40).ToArray();
        var (result, _) = await SortTestHelper.RunAsync(new GnomeSort(), input);

        Assert.Equal(input.Length - 1, result.Statistics.Comparisons);
        Assert.Equal(0, result.Statistics.Swaps);
    }

    [Fact]
    public async Task GnomeSort_EqualValuesAreStableAndNotSwapped()
    {
        var input = Enumerable.Repeat(7, 40).ToArray();
        var (result, _) = await SortTestHelper.RunAsync(new GnomeSort(), input);

        Assert.All(result.SortedValues, value => Assert.Equal(7, value));
        Assert.Equal(0, result.Statistics.Swaps);
    }

    [Fact]
    public async Task GnomeSort_LargeArrayIsSorted()
    {
        var random = new Random(2468);
        var input = Enumerable.Range(0, 200).Select(_ => random.Next(-500, 500)).ToArray();
        var (result, _) = await SortTestHelper.RunAsync(new GnomeSort(), input);

        SortTestHelper.AssertSorted(result.SortedValues);
        SortTestHelper.AssertSameMultiset(input, result.SortedValues);
    }

    [Fact]
    public async Task GnomeSort_CancellationStopsAlgorithm()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GnomeSort().SortAsync(
                new[] { 3, 2, 1 },
                new Core.Execution.CollectingStepSink<ArraySortStep>(),
                source.Token));
    }
}
