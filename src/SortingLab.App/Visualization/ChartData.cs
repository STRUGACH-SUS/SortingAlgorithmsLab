namespace SortingLab.App.Visualization;

public enum BarState
{
    Normal,
    Comparing,
    Active,
    Sorted,
    Excluded,
    Result,
    Range,
    LeftRange,
    RightRange
}

public readonly record struct BarDatum(int Value, BarState State, int Index = -1);

public readonly record struct BenchmarkPoint(int WordCount, double QuickSortMs, double RadixMs);
