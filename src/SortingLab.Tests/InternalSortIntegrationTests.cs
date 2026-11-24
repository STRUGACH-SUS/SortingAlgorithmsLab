using SortingLab.App.ViewModels.Sections;

namespace SortingLab.Tests;

public sealed class InternalSortIntegrationTests
{
    [Fact]
    public void AllThreeInternalAlgorithmsAreAvailable()
    {
        var viewModel = new InternalSortViewModel();

        Assert.Equal(
            new[] { "odd-even", "tournament", "gnome" },
            viewModel.Algorithms.Select(choice => choice.Descriptor.Id));
    }

    [Fact]
    public void SelectingGnomeUpdatesVisualizationState()
    {
        var viewModel = new InternalSortViewModel();

        viewModel.SelectedAlgorithm = viewModel.Algorithms.Single(choice => choice.Descriptor.Id == "gnome");
        Assert.True(viewModel.IsGnome);
        Assert.Contains("Gnome", viewModel.InputHint);
        Assert.Equal(0, viewModel.SelectedVisualizationTab);
    }
}
