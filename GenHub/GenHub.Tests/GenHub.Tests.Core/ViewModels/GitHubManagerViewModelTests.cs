using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Github;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.Factories;
using GenHub.Features.GitHub.Services;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Tests for GitHubManagerViewModel.
/// </summary>
public class GitHubManagerViewModelTests
{
    private readonly Mock<IGitHubServiceFacade> _serviceFacadeMock;
    private readonly Mock<GitHubDisplayItemFactory> _itemFactoryMock;
    private readonly Mock<GitHubItemsTreeViewModel> _itemsTreeViewModelMock;
    private readonly Mock<GitHubDetailsViewModel> _detailsViewModelMock;
    private readonly Mock<InstallationViewModel> _installationViewModelMock;
    private readonly Mock<RepositoryControlViewModel> _repositoryControlViewModelMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<GitHubManagerViewModel>> _loggerMock;
    private readonly GitHubManagerViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubManagerViewModelTests"/> class.
    /// </summary>
    public GitHubManagerViewModelTests()
    {
        _serviceFacadeMock = new Mock<IGitHubServiceFacade>();
        _repositoryControlViewModelMock = new Mock<RepositoryControlViewModel>(_serviceFacadeMock.Object, Mock.Of<ILogger<RepositoryControlViewModel>>());
        _itemFactoryMock = new Mock<GitHubDisplayItemFactory>(_serviceFacadeMock.Object, Mock.Of<ILogger<GitHubDisplayItemFactory>>());
        _itemsTreeViewModelMock = new Mock<GitHubItemsTreeViewModel>(_serviceFacadeMock.Object, Mock.Of<ILogger<GitHubItemsTreeViewModel>>(), _repositoryControlViewModelMock.Object);
        _detailsViewModelMock = new Mock<GitHubDetailsViewModel>(_serviceFacadeMock.Object, new Mock<ILogger<GitHubDetailsViewModel>>().Object);
        _installationViewModelMock = new Mock<InstallationViewModel>(
            _serviceFacadeMock.Object,
            Mock.Of<ICasService>(),
            Mock.Of<IContentStorageService>(),
            Mock.Of<IContentManifestBuilder>(),
            Mock.Of<IGitHubContentProcessor>(),
            Mock.Of<IContentOrchestrator>(),
            Mock.Of<IContentManifestPool>(),
            Mock.Of<IDependencyResolver>(),
            new Mock<ILogger<InstallationViewModel>>().Object);
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<GitHubManagerViewModel>>();

        _viewModel = new GitHubManagerViewModel(
            _serviceFacadeMock.Object,
            _itemFactoryMock.Object,
            _itemsTreeViewModelMock.Object,
            _detailsViewModelMock.Object,
            _installationViewModelMock.Object,
            _repositoryControlViewModelMock.Object,
            _serviceProviderMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that constructor initializes properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Assert
        Assert.False(_viewModel.IsLoading);
        Assert.Equal("Ready", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsRepositoryValid);
        Assert.NotNull(_viewModel.Items);
        Assert.Empty(_viewModel.Items);
        Assert.Equal(_itemsTreeViewModelMock.Object, _viewModel.ItemsTreeViewModel);
        Assert.Equal(_detailsViewModelMock.Object, _viewModel.DetailsViewModel);
        Assert.Equal(_installationViewModelMock.Object, _viewModel.InstallationViewModel);
        Assert.Equal(_repositoryControlViewModelMock.Object, _viewModel.RepositoryControlVM);
    }

    /// <summary>
    /// Verifies that ClearRepository resets repository state.
    /// </summary>
    [Fact]
    public void ClearRepository_ResetsRepositoryState()
    {
        // Arrange
        _viewModel.IsRepositoryValid = true;
        _viewModel.StatusMessage = "Loaded";
        var mockItem = new Mock<GitHubDisplayItemViewModel>(new Mock<ILogger>().Object);
        mockItem.SetupGet(x => x.DisplayName).Returns("Test Item");
        mockItem.SetupGet(x => x.Description).Returns("Test Description");
        mockItem.SetupGet(x => x.SortDate).Returns(DateTime.Now);
        mockItem.SetupGet(x => x.IsExpandable).Returns(false);
        mockItem.SetupGet(x => x.IsRelease).Returns(false);
        _viewModel.Items.Add(mockItem.Object);

        // Act
        _viewModel.GetType().GetMethod("ClearRepository", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_viewModel, null);

        // Assert
        Assert.False(_viewModel.IsRepositoryValid);
        Assert.Equal("Ready", _viewModel.StatusMessage);
        Assert.Empty(_viewModel.Items);
    }
}
