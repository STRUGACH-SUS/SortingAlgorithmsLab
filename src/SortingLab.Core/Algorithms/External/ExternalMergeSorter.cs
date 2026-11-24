using SortingLab.Core.Abstractions;
using SortingLab.Core.Models;

namespace SortingLab.Core.Algorithms.External;

/// <summary>
/// Прямое внешнее слияние: начальные серии состоят из одного элемента.
/// Использует тот же файловый конвейер, что и Natural Merge Sort,
/// но не ищет уже существующие серии.
/// </summary>
public sealed class ExternalMergeSorter : IExternalSortAlgorithm
{
    private readonly NaturalMergeSorter _inner = new(ExternalMergeMode.Direct);

    public AlgorithmDescriptor Descriptor => NaturalMergeSorter.DirectDescriptor;

    public Task<ExternalSortResult> SortAsync(
        ExternalSortRequest request,
        IStepSink<ExternalSortStep> stepSink,
        CancellationToken cancellationToken = default) =>
        _inner.SortAsync(request, stepSink, cancellationToken);
}
