using System.Collections.ObjectModel;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Tests for GitHubItemsTreeViewModel.
/// </summary>
public class GitHubItemsTreeViewModelTests
{
    private readonly Mock<IGitHubServiceFacade> _gitHubServiceMock;
    private readonly Mock<ILogger<GitHubItemsTreeViewModel>> _loggerMock;
    private readonly RepositoryControlViewModel _repositoryControl;
    private readonly GitHubItemsTreeViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubItemsTreeViewModelTests"/> class.
    /// </summary>
    public GitHubItemsTreeViewModelTests()
    {
        _gitHubServiceMock = new Mock<IGitHubServiceFacade>();
        _loggerMock = new Mock<ILogger<GitHubItemsTreeViewModel>>();
        var repoControlLoggerMock = new Mock<ILogger<RepositoryControlViewModel>>();
        _repositoryControl = new RepositoryControlViewModel(_gitHubServiceMock.Object, repoControlLoggerMock.Object);
        _viewModel = new GitHubItemsTreeViewModel(_gitHubServiceMock.Object, _loggerMock.Object, _repositoryControl);
    }

    /// <summary>
    /// Verifies that constructor initializes properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Assert
        Assert.NotNull(_viewModel.Items);
        Assert.Empty(_viewModel.Items);
        Assert.Null(_viewModel.SelectedItem);
    }

    /// <summary>
    /// Verifies that constructor throws ArgumentNullException when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubItemsTreeViewModel(_gitHubServiceMock.Object, null!, _repositoryControl));
    }

    /// <summary>
    /// Verifies that SetItems method sets items correctly.
    /// </summary>
    [Fact]
    public void SetItems_SetsItemsCorrectly()
    {
        // Arrange
        var items = new ObservableCollection<GitHubDisplayItemViewModel>
        {
            new GitHubReleaseDisplayItemViewModel(
                new GitHubRelease { TagName = "v1.0", Name = "Release 1" },
                Mock.Of<IGitHubServiceFacade>(),
                "owner",
                "repo",
                Mock.Of<ILogger>()),
        };

        // Act
        _viewModel.SetItems(items);

        // Assert
        Assert.Single(_viewModel.Items);
        Assert.Equal("Releases", _viewModel.Items[0].DisplayName);
        Assert.True(_viewModel.Items[0].IsFolder);
        Assert.Single(_viewModel.Items[0].Children);
    }

    /// <summary>
    /// Verifies that SetItems method sets empty collection when items is null.
    /// </summary>
    [Fact]
    public void SetItems_WithNullItems_SetsEmptyCollection()
    {
#pragma warning disable CS8625
        // Act
        _viewModel.SetItems(default);
#pragma warning restore CS8625

        // Assert
        Assert.NotNull(_viewModel.Items);
        Assert.Empty(_viewModel.Items);
    }

    /// <summary>
    /// Verifies that ClearItems method clears all items and resets properties.
    /// </summary>
    [Fact]
    public void ClearItems_ClearsAllItemsAndResetsProperties()
    {
        // Arrange
        var items = new ObservableCollection<GitHubDisplayItemViewModel>
        {
            new GitHubReleaseDisplayItemViewModel(
                new GitHubRelease { TagName = "v1.0.0", Name = "Release 1" },
                Mock.Of<IGitHubServiceFacade>(),
                "owner",
                "repo",
                Mock.Of<ILogger>()),
        };
        _viewModel.SetItems(items);
        _viewModel.SelectedItem = _viewModel.Items.First();

        // Act
        _viewModel.ClearItems();

        // Assert
        Assert.Empty(_viewModel.Items);
        Assert.Null(_viewModel.SelectedItem);
    }
}
