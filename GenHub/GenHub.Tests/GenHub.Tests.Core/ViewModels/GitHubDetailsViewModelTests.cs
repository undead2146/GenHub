using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Tests for GitHubDetailsViewModel.
/// </summary>
public class GitHubDetailsViewModelTests
{
    private readonly Mock<IGitHubServiceFacade> _gitHubServiceMock;
    private readonly Mock<ILogger<GitHubDetailsViewModel>> _loggerMock;
    private readonly GitHubDetailsViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubDetailsViewModelTests"/> class.
    /// </summary>
    public GitHubDetailsViewModelTests()
    {
        _gitHubServiceMock = new Mock<IGitHubServiceFacade>();
        _loggerMock = new Mock<ILogger<GitHubDetailsViewModel>>();
        _viewModel = new GitHubDetailsViewModel(_gitHubServiceMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that constructor initializes properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Assert
        Assert.NotNull(_viewModel.Details);
        Assert.Empty(_viewModel.Details);
        Assert.Null(_viewModel.SelectedItem);
        Assert.False(_viewModel.IsLoading);
        Assert.Equal("Select an item to view details", _viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that constructor throws exception when service is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubDetailsViewModel(null!, _loggerMock.Object));
    }

    /// <summary>
    /// Verifies that constructor throws exception when logger is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubDetailsViewModel(_gitHubServiceMock.Object, null!));
    }

    /// <summary>
    /// Verifies that SetSelectedItem populates details for a release item.
    /// </summary>
    [Fact]
    public void SetSelectedItem_PopulatesDetailsForRelease()
    {
        // Arrange
        var release = new GitHubRelease
        {
            TagName = "v1.0.0",
            Name = "Test Release",
            PublishedAt = DateTimeOffset.Now,
        };
        var releaseItemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        releaseItemMock.SetupGet(x => x.DisplayName).Returns("Test Release");
        releaseItemMock.SetupGet(x => x.Description).Returns("Release description");
        releaseItemMock.SetupGet(x => x.SortDate).Returns(DateTime.Now);
        releaseItemMock.SetupGet(x => x.IsRelease).Returns(true);
        releaseItemMock.SetupGet(x => x.IsWorkflowRun).Returns(false);

        // Act
        _viewModel.SetSelectedItem(releaseItemMock.Object);

        // Assert
        Assert.Equal(releaseItemMock.Object, _viewModel.SelectedItem);
        Assert.NotEmpty(_viewModel.Details);
        var nameDetail = _viewModel.Details.FirstOrDefault(d => d.Label == "Name");
        Assert.NotNull(nameDetail);
        Assert.Equal("Test Release", nameDetail.Value);
        var typeDetail = _viewModel.Details.FirstOrDefault(d => d.Label == "Type");
        Assert.NotNull(typeDetail);
        Assert.Equal("Release", typeDetail.Value);
    }

    /// <summary>
    /// Verifies that SetSelectedItem populates details for a workflow item.
    /// </summary>
    [Fact]
    public void SetSelectedItem_PopulatesDetailsForWorkflow()
    {
        // Arrange
        var workflowRun = new GitHubWorkflowRun
        {
            Id = 1,
            RunNumber = 123,
            Workflow = new GitHubWorkflow { Name = "CI Workflow" },
            CreatedAt = DateTimeOffset.Now,
        };
        var workflowItemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        workflowItemMock.SetupGet(x => x.DisplayName).Returns("CI Workflow");
        workflowItemMock.SetupGet(x => x.Description).Returns("Workflow description");
        workflowItemMock.SetupGet(x => x.SortDate).Returns(DateTime.Now);
        workflowItemMock.SetupGet(x => x.IsRelease).Returns(false);
        workflowItemMock.SetupGet(x => x.IsWorkflowRun).Returns(true);
        workflowItemMock.SetupGet(x => x.RunNumber).Returns(123);

        // Act
        _viewModel.SetSelectedItem(workflowItemMock.Object);

        // Assert
        Assert.Equal(workflowItemMock.Object, _viewModel.SelectedItem);
        Assert.NotEmpty(_viewModel.Details);
        var nameDetail = _viewModel.Details.FirstOrDefault(d => d.Label == "Name");
        Assert.NotNull(nameDetail);
        Assert.Equal("CI Workflow", nameDetail.Value);
        var typeDetail = _viewModel.Details.FirstOrDefault(d => d.Label == "Type");
        Assert.NotNull(typeDetail);
        Assert.Equal("Workflow Run", typeDetail.Value);
    }

    /// <summary>
    /// Verifies that SetSelectedItem handles null item.
    /// </summary>
    [Fact]
    public void SetSelectedItem_HandlesNullItem()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        _viewModel.SetSelectedItem(itemMock.Object);

        // Act
        _viewModel.SetSelectedItem(null);

        // Assert
        Assert.Null(_viewModel.SelectedItem);
        Assert.Empty(_viewModel.Details);
        Assert.Equal("Select an item to view details", _viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that ClearSelection clears all details.
    /// </summary>
    [Fact]
    public void ClearSelection_ClearsAllDetails()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        itemMock.SetupGet(x => x.DisplayName).Returns("Test Release");
        itemMock.SetupGet(x => x.IsRelease).Returns(true);
        _viewModel.SetSelectedItem(itemMock.Object);

        // Act
        _viewModel.ClearSelection();

        // Assert
        Assert.Empty(_viewModel.Details);
        Assert.Null(_viewModel.SelectedItem);
        Assert.Equal("Select an item to view details", _viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that SelectedItem setter raises PropertyChanged event.
    /// </summary>
    [Fact]
    public void SelectedItem_PropertyChanged_IsRaised()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.SelectedItem))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        _viewModel.SelectedItem = itemMock.Object;

        // Assert
        Assert.True(propertyChangedRaised);
    }

    /// <summary>
    /// Verifies that SetSelectedItem with valid item loads details and sets status.
    /// </summary>
    [Fact]
    public void SetSelectedItem_WithValidItem_LoadsDetailsAndSetsStatus()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        itemMock.SetupGet(x => x.DisplayName).Returns("Test Item");
        itemMock.SetupGet(x => x.Description).Returns("Test Description");
        itemMock.SetupGet(x => x.SortDate).Returns(DateTime.Now);
        itemMock.SetupGet(x => x.IsRelease).Returns(true);

        // Act
        _viewModel.SetSelectedItem(itemMock.Object);

        // Assert
        Assert.Equal(itemMock.Object, _viewModel.SelectedItem);
        Assert.Equal("Viewing details for Test Item", _viewModel.StatusMessage);
        Assert.NotEmpty(_viewModel.Details);
        Assert.Contains(_viewModel.Details, d => d.Label == "Name" && d.Value == "Test Item");
    }

    /// <summary>
    /// Verifies that SetSelectedItem with null item clears details and sets status.
    /// </summary>
    [Fact]
    public void SetSelectedItem_WithNullItem_ClearsDetailsAndSetsStatus()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        _viewModel.SetSelectedItem(itemMock.Object);
        _viewModel.Details.Add(new DetailItem("Test", "Value"));

        // Act
        _viewModel.SetSelectedItem(null);

        // Assert
        Assert.Null(_viewModel.SelectedItem);
        Assert.Equal("Select an item to view details", _viewModel.StatusMessage);
        Assert.Empty(_viewModel.Details);
    }

    /// <summary>
    /// Verifies that DownloadAsync with downloadable item calls download and updates status.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DownloadAsync_WithDownloadableItem_CallsDownloadAndUpdatesStatus()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        itemMock.SetupGet(x => x.CanDownload).Returns(true);
        itemMock.SetupGet(x => x.DisplayName).Returns("Test Item");

        _viewModel.SelectedItem = itemMock.Object;

        // Act
        await _viewModel.DownloadCommand.ExecuteAsync(null);

        // Assert
        itemMock.Verify(x => x.DownloadAsync(), Times.Once);
        Assert.Equal("Download completed", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsLoading);
    }

    /// <summary>
    /// Verifies that InstallAsync with installable item calls install and updates status.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InstallAsync_WithInstallableItem_CallsInstallAndUpdatesStatus()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        itemMock.SetupGet(x => x.CanInstall).Returns(true);
        itemMock.SetupGet(x => x.DisplayName).Returns("Test Item");

        _viewModel.SelectedItem = itemMock.Object;

        // Act
        await _viewModel.InstallCommand.ExecuteAsync(null);

        // Assert
        itemMock.Verify(x => x.InstallAsync(), Times.Once);
        Assert.Equal("Installation completed", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsLoading);
    }

    /// <summary>
    /// Verifies that ClearSelection clears selection and details.
    /// </summary>
    [Fact]
    public void ClearSelection_ClearsSelectionAndDetails()
    {
        // Arrange
        var itemMock = new Mock<GitHubDisplayItemViewModel>(_loggerMock.Object);
        _viewModel.SetSelectedItem(itemMock.Object);
        _viewModel.Details.Add(new DetailItem("Test", "Value"));

        // Act
        _viewModel.ClearSelection();

        // Assert
        Assert.Null(_viewModel.SelectedItem);
        Assert.Equal("Select an item to view details", _viewModel.StatusMessage);
        Assert.Empty(_viewModel.Details);
    }
}
