using SortingLab.Core.Abstractions;
using SortingLab.Core.Csv;
using SortingLab.Core.Models;
using SortingLab.Core.Text;

namespace SortingLab.App.ViewModels.Common;

public sealed record AlgorithmChoice(IArraySortAlgorithm Algorithm, AlgorithmDescriptor Descriptor)
{
    public string Name => Descriptor.Name;
}

public sealed record ExternalAlgorithmChoice(IExternalSortAlgorithm Algorithm, AlgorithmDescriptor Descriptor)
{
    public string Name => Descriptor.Name;
}

public sealed record TextAlgorithmChoice(ITextSortAlgorithm Algorithm, AlgorithmDescriptor Descriptor)
{
    public string Name => Descriptor.Name;
}

public sealed record ColumnOption(int Index, string Name)
{
    public override string ToString() => Name;
}

public sealed record KeyTypeOption(SortKeyType Value, string Name);

public sealed record DirectionOption(SortDirection Value, string Name);

public sealed record LanguageOption(TextLanguage Value, string Name);
