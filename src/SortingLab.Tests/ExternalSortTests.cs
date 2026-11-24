using SortingLab.Core.Abstractions;
using SortingLab.Core.Algorithms.External;
using SortingLab.Core.Csv;
using SortingLab.Core.Execution;
using SortingLab.Core.Models;

namespace SortingLab.Tests;

public sealed class ExternalSortTests
{
    [Fact]
    public async Task EmptyTable_IsHandled()
    {
        var table = new CsvTable(
            new[] { "Страна", "Население" },
            Array.Empty<CsvRecord>());

        var (result, steps, root) = await RunAsync(table, new SortKey(1, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.Empty(result.SortedRecords);
            Assert.True(steps[0].IsInitial);
            Assert.True(steps[^1].IsCompleted);
            Assert.Empty(Directory.GetDirectories(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SingleRecord_IsReturnedUnchanged()
    {
        var table = CreateTable(new[] { (1, "Альфа", 10) });
        var (result, _, root) = await RunAsync(table, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.Single(result.SortedRecords);
            Assert.Equal(1, result.SortedRecords[0].Id);
            Assert.Equal("Альфа", result.SortedRecords[0].Field(1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AlreadySortedTable_NeedsNoMergePass()
    {
        var table = CreateTable(new[] { (1, "Альфа", 10), (2, "Бета", 20), (3, "Гамма", 30) });
        var (result, steps, root) = await RunAsync(table, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.Equal(new[] { 1, 2, 3 }, result.SortedRecords.Select(record => record.Id));
            Assert.Equal(1, result.InitialRunCount);
            Assert.Equal(0, result.PassCount);
            Assert.Contains(steps, step => step.Kind == ExternalStepKind.RunsCompleted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReverseTable_IsSortedWithMultiplePasses()
    {
        var table = CreateTable(new[]
        {
            (1, "Альфа", 50), (2, "Бета", 40), (3, "Гамма", 30), (4, "Дельта", 20), (5, "Эпсилон", 10)
        });

        var (result, steps, root) = await RunAsync(table, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.Equal(new[] { 5, 4, 3, 2, 1 }, result.SortedRecords.Select(record => record.Id));
            Assert.True(result.InitialRunCount > 1);
            Assert.True(result.PassCount > 0);
            Assert.True(result.Statistics.Comparisons > 0);
            Assert.Contains(steps, step => step.Kind == ExternalStepKind.MergeStarted);
            Assert.Contains(steps, step => step.Kind == ExternalStepKind.WriteRecord);
            Assert.Contains(steps, step => step.Kind == ExternalStepKind.FileWrite);
            Assert.True(result.Statistics.Reads > 0);
            Assert.True(result.Statistics.Writes > 0);
            Assert.Equal(steps.Count, steps[^1].Number);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EqualKeys_KeepOriginalOrder_Stable()
    {
        var table = CreateTable(new[]
        {
            (1, "Первый", 1), (2, "Второй", 2), (3, "Третий", 1), (4, "Четвёртый", 2)
        });

        var (result, _, root) = await RunAsync(table, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.Equal(new[] { 1, 3, 2, 4 }, result.SortedRecords.Select(record => record.Id));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DescendingDirection_IsSupported()
    {
        var table = CreateTable(new[] { (1, "Альфа", 10), (2, "Бета", 30), (3, "Гамма", 20) });
        var (result, _, root) = await RunAsync(table, new SortKey(2, SortKeyType.Integer, SortDirection.Descending));
        try
        {
            Assert.Equal(new[] { 2, 3, 1 }, result.SortedRecords.Select(record => record.Id));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DecimalKey_IsSupported()
    {
        var table = CreateTable(new[] { (1, "Альфа", 1), (2, "Бета", 2), (3, "Гамма", 3) }, areas: new[] { "2,5", "1.5", "3.25" });
        var (result, _, root) = await RunAsync(table, new SortKey(3, SortKeyType.Decimal, SortDirection.Ascending));
        try
        {
            Assert.Equal(new[] { 2, 1, 3 }, result.SortedRecords.Select(record => record.Id));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TextKey_IsCaseInsensitive()
    {
        var table = CreateTable(new[] { (1, "яблоко", 1), (2, "Апельсин", 2), (3, "банан", 3) });
        var (result, _, root) = await RunAsync(table, new SortKey(1, SortKeyType.Text, SortDirection.Ascending));
        try
        {
            Assert.Equal(new[] { 2, 3, 1 }, result.SortedRecords.Select(record => record.Id));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AllColumns_ArePreserved()
    {
        var table = new CsvTable(
            new[] { "Код", "Страна", "Континент", "Столица", "Площадь", "Население" },
            new[]
            {
                new CsvRecord(0, new[] { "A1", "Страна А", "Европа", "Город А", "10,5", "100" }),
                new CsvRecord(1, new[] { "B2", "Страна Б", "Азия", "Город Б", "20.25", "50" })
            });

        var (result, _, root) = await RunAsync(table, new SortKey(5, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.Equal(2, result.SortedRecords.Count);
            Assert.All(result.SortedRecords, record => Assert.Equal(table.Headers.Count, record.Fields.Count));
            var original = table.Records.ToDictionary(record => record.Id);
            foreach (var record in result.SortedRecords)
            {
                Assert.Equal(original[record.Id].Fields, record.Fields);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidNumericKey_ThrowsCsvFormatException()
    {
        var table = CreateTable(new[] { (1, "Альфа", 10) });
        table = table.WithRecords(new[]
        {
            new CsvRecord(0, new[] { "1", "Альфа", "не число", "10", "—" })
        });

        var sink = new CollectingStepSink<ExternalSortStep>();
        await Assert.ThrowsAsync<CsvFormatException>(() =>
            new NaturalMergeSorter().SortAsync(
                new ExternalSortRequest(table.Headers, table.Records, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending)),
                sink));
    }

    [Fact]
    public async Task Cancellation_CleansTempDirectory()
    {
        var table = CreateTable(new[] { (1, "Альфа", 10), (2, "Бета", 20) });
        var root = CreateRoot();
        try
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            var sink = new CollectingStepSink<ExternalSortStep>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new NaturalMergeSorter().SortAsync(
                    new ExternalSortRequest(table.Headers, table.Records, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending), root),
                    sink,
                    source.Token));

            Assert.Empty(Directory.GetDirectories(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task TempDirectory_IsRemovedAfterSuccess()
    {
        var table = CreateTable(new[] { (1, "Альфа", 10), (2, "Бета", 20) });
        var (result, _, root) = await RunAsync(table, new SortKey(2, SortKeyType.Integer, SortDirection.Ascending));
        try
        {
            Assert.False(Directory.Exists(result.TempDirectory));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ExternalMergeSort_CreatesOneElementRunsAndSorts()
    {
        var table = CreateTable(new[]
        {
            (1, "Альфа", 10), (2, "Бета", 20), (3, "Гамма", 30), (4, "Дельта", 40), (5, "Эпсилон", 50)
        });

        var (result, steps, root) = await RunAsync(
            table,
            new SortKey(2, SortKeyType.Integer, SortDirection.Ascending),
            new ExternalMergeSorter());
        try
        {
            Assert.Equal(5, result.InitialRunCount);
            Assert.True(result.PassCount > 0);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.SortedRecords.Select(record => record.Id));
            Assert.Contains(steps, step =>
                step.Kind == ExternalStepKind.RunDetected &&
                step.Explanation.Contains("из одного элемента", StringComparison.Ordinal));
            Assert.False(Directory.Exists(result.TempDirectory));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ExternalMergeSort_IsStableForEqualKeys()
    {
        var table = CreateTable(new[]
        {
            (1, "Первый", 1), (2, "Второй", 2), (3, "Третий", 1), (4, "Четвёртый", 2)
        });

        var (result, _, root) = await RunAsync(
            table,
            new SortKey(2, SortKeyType.Integer, SortDirection.Ascending),
            new ExternalMergeSorter());
        try
        {
            Assert.Equal(new[] { 1, 3, 2, 4 }, result.SortedRecords.Select(record => record.Id));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ExternalAlgorithmsHaveDistinctDescriptors()
    {
        Assert.NotEqual(
            new NaturalMergeSorter().Descriptor.Id,
            new ExternalMergeSorter().Descriptor.Id);
        Assert.Equal("external-merge", new ExternalMergeSorter().Descriptor.Id);
    }

    private static CsvTable CreateTable(
        IEnumerable<(int Id, string Name, int Population)> rows,
        IReadOnlyList<string>? areas = null)
    {
        var records = new List<CsvRecord>();
        var index = 0;
        foreach (var row in rows)
        {
            var area = areas is not null && index < areas.Count ? areas[index] : row.Population.ToString();
            records.Add(new CsvRecord(row.Id, new[]
            {
                row.Id.ToString(), row.Name, row.Population.ToString(), area, "1000"
            }));
            index++;
        }

        return new CsvTable(
            new[] { "Код", "Страна", "Население", "Площадь", "Дополнительно" },
            records);
    }

    private static async Task<(ExternalSortResult Result, IReadOnlyList<ExternalSortStep> Steps, string Root)> RunAsync(
        CsvTable table,
        SortKey key,
        IExternalSortAlgorithm? algorithm = null)
    {
        var root = CreateRoot();
        var sink = new CollectingStepSink<ExternalSortStep>();
        var result = await (algorithm ?? new NaturalMergeSorter()).SortAsync(
            new ExternalSortRequest(table.Headers, table.Records, key, root),
            sink);
        return (result, sink.Steps, root);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "SortingLabTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
