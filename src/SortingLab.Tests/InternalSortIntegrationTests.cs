using SortingLab.App.ViewModels.Sections;

namespace SortingLab.Tests;

public sealed class InternalSortIntegrationTests
{
    [Fact]
    public void AllFourInternalAlgorithmsAreAvailable()
    {
        var viewModel = new InternalSortViewModel();

        Assert.Equal(
            new[] { "odd-even", "tournament", "gnome", "merge" },
            viewModel.Algorithms.Select(choice => choice.Descriptor.Id));
    }

    [Fact]
    public void SelectingNewAlgorithmsUpdatesVisualizationState()
    {
        var viewModel = new InternalSortViewModel();

        viewModel.SelectedAlgorithm = viewModel.Algorithms.Single(choice => choice.Descriptor.Id == "gnome");
        Assert.True(viewModel.IsGnome);
        Assert.False(viewModel.IsMerge);
        Assert.Contains("Gnome", viewModel.InputHint);

        viewModel.SelectedAlgorithm = viewModel.Algorithms.Single(choice => choice.Descriptor.Id == "merge");
        Assert.True(viewModel.IsMerge);
        Assert.False(viewModel.IsGnome);
        Assert.Contains("Merge", viewModel.InputHint);
        Assert.Equal(0, viewModel.SelectedVisualizationTab);
    }
}
