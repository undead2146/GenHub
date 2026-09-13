using System.Linq;
using GenHub.Core.Constants;
using GenHub.Core.Models.Content;
using GenHub.Features.Downloads.ViewModels.Filters;
using Xunit;

namespace GenHub.Tests.Core.Features.Downloads.Filters;

/// <summary>
/// Tests ModDB-specific section and filter functionality.
/// </summary>
public sealed class ModDBFilterViewModelTests
{
    /// <summary>
    /// Verifies that a freshly constructed view model has no active filters.
    /// </summary>
    [Fact]
    public void HasActiveFilters_Default_False()
    {
        var viewModel = new ModDBFilterViewModel();

        Assert.False(viewModel.HasActiveFilters);
        Assert.Equal(ModDBSection.Downloads, viewModel.SelectedSection);
        Assert.True(viewModel.IsDownloadsSelected);
        Assert.False(viewModel.IsAddonsSelected);
        Assert.False(viewModel.IsModsSelected);
    }

    /// <summary>
    /// Verifies that setting filters updates active state and emits correct query parameters.
    /// </summary>
    [Fact]
    public void ApplyFilters_WithFilters_CopiesModDBSpecificQueryValues()
    {
        var viewModel = new ModDBFilterViewModel
        {
            SelectedCategory = "1",
            SelectedAddonCategory = "101",
            SelectedTimeframe = "2",
            SelectedSort = ModDBConstants.SortRatingDesc,
        };

        var query = viewModel.ApplyFilters(new ContentSearchQuery());

        Assert.True(viewModel.HasActiveFilters);
        Assert.Equal("downloads", query.ModDBSection);
        Assert.Equal("1", query.ModDBCategory);
        Assert.Equal("101", query.ModDBAddonCategory);
        Assert.Equal("2", query.ModDBTimeframe);
        Assert.Equal(ModDBConstants.SortRatingDesc, query.Sort);
    }

    /// <summary>
    /// Verifies that switching sections updates visibility properties and clears active filters.
    /// </summary>
    [Fact]
    public void SetSection_SwitchesSectionAndClearsFilters()
    {
        var viewModel = new ModDBFilterViewModel
        {
            SelectedCategory = "1",
        };
        Assert.True(viewModel.HasActiveFilters);

        viewModel.SetSectionCommand.Execute(ModDBSection.Addons);

        Assert.Equal(ModDBSection.Addons, viewModel.SelectedSection);
        Assert.True(viewModel.IsAddonsSelected);
        Assert.False(viewModel.IsDownloadsSelected);
        Assert.False(viewModel.ShowCategoryFilter);
        Assert.True(viewModel.ShowAddonCategoryFilter);
        Assert.True(viewModel.ShowLicenseFilter);
        Assert.False(viewModel.HasActiveFilters);
    }

    /// <summary>
    /// Verifies that clearing filters resets all selections.
    /// </summary>
    [Fact]
    public void ClearFilters_ResetsSelections()
    {
        var viewModel = new ModDBFilterViewModel
        {
            SelectedCategory = "1",
            SelectedAddonCategory = "100",
            SelectedLicense = "8",
            SelectedTimeframe = "3",
            SelectedSort = ModDBConstants.SortRatingDesc,
        };

        Assert.True(viewModel.HasActiveFilters);

        viewModel.ClearFilters();

        Assert.False(viewModel.HasActiveFilters);
        Assert.True(string.IsNullOrEmpty(viewModel.SelectedCategory));
        Assert.True(string.IsNullOrEmpty(viewModel.SelectedAddonCategory));
        Assert.True(string.IsNullOrEmpty(viewModel.SelectedLicense));
        Assert.True(string.IsNullOrEmpty(viewModel.SelectedTimeframe));
        Assert.Equal(ModDBConstants.DefaultSort, viewModel.SelectedSort);
    }

    /// <summary>
    /// Verifies that active filter summaries are correctly generated.
    /// </summary>
    [Fact]
    public void GetActiveFilterSummary_ReturnsSummaryItems()
    {
        var viewModel = new ModDBFilterViewModel
        {
            SelectedCategory = "1",
            SelectedSort = ModDBConstants.SortRatingDesc,
        };

        var summaries = viewModel.GetActiveFilterSummary().ToList();

        Assert.Contains("Category: 1", summaries);
        Assert.Contains($"Sort: {ModDBConstants.SortRatingDesc}", summaries);
    }
}
