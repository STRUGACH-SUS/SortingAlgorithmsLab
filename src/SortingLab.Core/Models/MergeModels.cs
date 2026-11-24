namespace SortingLab.Core.Models;

public enum MergeRangeState
{
    Pending,
    Splitting,
    Merging,
    Sorted
}

/// <summary>
/// Один диапазон в дереве рекурсивного деления Merge Sort.
/// </summary>
public sealed record MergeRangeSnapshot(int Start, int End, int Depth, MergeRangeState State)
{
    public int Length => End - Start + 1;
}

/// <summary>
/// Снимок уровней рекурсии Merge Sort: все диапазоны с их состоянием.
/// </summary>
public sealed record MergeLevelsSnapshot(int ArrayLength, IReadOnlyList<MergeRangeSnapshot> Ranges)
{
    public int MaxDepth => Ranges.Count == 0 ? 0 : Ranges.Max(range => range.Depth);
}
