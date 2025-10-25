using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Github;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;

namespace GenHub.Tests.Core.ViewModels;

/// <summary>
/// Tests for InstallationViewModel.
/// </summary>
public class InstallationViewModelTests
{
    private readonly Mock<IGitHubServiceFacade> _gitHubServiceMock;
    private readonly Mock<ICasService> _casServiceMock;
    private readonly Mock<IContentStorageService> _contentStorageServiceMock;
    private readonly Mock<IContentManifestBuilder> _manifestBuilderMock;
    private readonly Mock<IGitHubContentProcessor> _gitHubContentProcessorMock;
    private readonly Mock<IContentOrchestrator> _contentOrchestratorMock;
    private readonly Mock<IContentManifestPool> _contentManifestPoolMock;
    private readonly Mock<IDependencyResolver> _dependencyResolverMock;
    private readonly Mock<ILogger<InstallationViewModel>> _loggerMock;
    private readonly InstallationViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallationViewModelTests"/> class.
    /// </summary>
    public InstallationViewModelTests()
    {
        _gitHubServiceMock = new Mock<IGitHubServiceFacade>();
        _casServiceMock = new Mock<ICasService>();
        _contentStorageServiceMock = new Mock<IContentStorageService>();
        _manifestBuilderMock = new Mock<IContentManifestBuilder>();
        _gitHubContentProcessorMock = new Mock<IGitHubContentProcessor>();
        _contentOrchestratorMock = new Mock<IContentOrchestrator>();
        _contentManifestPoolMock = new Mock<IContentManifestPool>();
        _dependencyResolverMock = new Mock<IDependencyResolver>();
        _loggerMock = new Mock<ILogger<InstallationViewModel>>();
        _viewModel = new InstallationViewModel(
            _gitHubServiceMock.Object,
            _casServiceMock.Object,
            _contentStorageServiceMock.Object,
            _manifestBuilderMock.Object,
            _gitHubContentProcessorMock.Object,
            _contentOrchestratorMock.Object,
            _contentManifestPoolMock.Object,
            _dependencyResolverMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that constructor initializes properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Assert
        Assert.False(_viewModel.IsInstalling);
        Assert.Equal(0, _viewModel.InstallationProgress);
        Assert.Equal("Ready to install", _viewModel.StatusMessage);
        Assert.Null(_viewModel.CurrentItem);
        Assert.NotNull(_viewModel.InstallationLog);
        Assert.Empty(_viewModel.InstallationLog);
    }

    /// <summary>
    /// Verifies that CancelInstallation resets installation state.
    /// </summary>
    [Fact]
    public void CancelInstallation_ResetsInstallationState()
    {
        // Arrange
        _viewModel.IsInstalling = true;
        _viewModel.InstallationProgress = 50;
        _viewModel.StatusMessage = "Installing...";
        _viewModel.InstallationLog.Add("Test log entry");

        // Act
        _viewModel.CancelInstallation();

        // Assert
        Assert.False(_viewModel.IsInstalling);
        Assert.Equal(0, _viewModel.InstallationProgress);
        Assert.Equal("Installation cancelled", _viewModel.StatusMessage);
        Assert.Contains("Installation cancelled", _viewModel.InstallationLog);
    }

    /// <summary>
    /// Verifies that InstallReleaseAssetAsync handles invalid parameters.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InstallReleaseAssetAsync_HandlesInvalidParameters()
    {
        // Act
        await _viewModel.InstallReleaseAssetAsync(string.Empty, "repo", new GitHubReleaseAsset());

        // Assert
        Assert.Equal("Invalid installation parameters", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsInstalling);
    }

    /// <summary>
    /// Verifies that InstallArtifactAsync handles invalid parameters.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InstallArtifactAsync_HandlesInvalidParameters()
    {
        // Act
        await _viewModel.InstallArtifactAsync("owner", string.Empty, new GitHubArtifact());

        // Assert
        Assert.Equal("Invalid installation parameters", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsInstalling);
    }

    /// <summary>
    /// Verifies that IsInstalling setter raises PropertyChanged event.
    /// </summary>
    [Fact]
    public void IsInstalling_PropertyChanged_IsRaised()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_viewModel.IsInstalling))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        _viewModel.IsInstalling = true;

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.True(_viewModel.IsInstalling);
    }

    /// <summary>
    /// Verifies that InstallReleaseAssetAsync installs successfully with valid asset.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InstallReleaseAssetAsync_WithValidAsset_InstallsSuccessfully()
    {
        var asset = new GitHubReleaseAsset { Id = 1, Name = "test.zip" };
        var tempPath = Path.Combine(Path.GetTempPath(), "test.zip");

        _gitHubServiceMock.Setup(x => x.DownloadReleaseAssetAsync(It.IsAny<string>(), It.IsAny<string>(), asset, It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.CreateSuccess(Path.Combine(Path.GetTempPath(), "test.zip"), 1024, TimeSpan.FromSeconds(1)));

        _casServiceMock.Setup(x => x.StoreContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<string>.CreateSuccess("hash123"));

        // Act
        await _viewModel.InstallReleaseAssetAsync("owner", "repo", asset);

        // Assert
        Assert.True(_viewModel.InstallationProgress >= 50); // Should be at least 50 (after download) or 100 (if install succeeds)
        Assert.Contains("Installation completed", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsInstalling);
        Assert.Contains("Stored in CAS with hash: hash123", _viewModel.InstallationLog);
    }

    /// <summary>
    /// Verifies that InstallArtifactAsync installs successfully with valid artifact.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InstallArtifactAsync_WithValidArtifact_InstallsSuccessfully()
    {
        var artifact = new GitHubArtifact { Id = 1, Name = "test-artifact" };
        var tempPath = Path.Combine(Path.GetTempPath(), "test-artifact.zip");

        _gitHubServiceMock.Setup(x => x.DownloadArtifactAsync(It.IsAny<string>(), It.IsAny<string>(), artifact, It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.CreateSuccess(Path.Combine(Path.GetTempPath(), "test-artifact.zip"), 1024, TimeSpan.FromSeconds(1)));

        _casServiceMock.Setup(x => x.StoreContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<string>.CreateSuccess("hash456"));

        // Act
        await _viewModel.InstallArtifactAsync("owner", "repo", artifact);

        // Assert
        Assert.True(_viewModel.InstallationProgress >= 50); // Should be at least 50 (after download) or 100 (if install succeeds)
        Assert.Contains("Installation completed", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsInstalling);
        Assert.Contains("Stored in CAS with hash: hash456", _viewModel.InstallationLog);
    }

    /// <summary>
    /// Verifies that InstallReleaseAssetAsync handles error correctly when download fails.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InstallReleaseAssetAsync_WithDownloadFailure_HandlesError()
    {
        var asset = new GitHubReleaseAsset { Id = 1, Name = "test.zip" };

        _gitHubServiceMock.Setup(x => x.DownloadReleaseAssetAsync(It.IsAny<string>(), It.IsAny<string>(), asset, It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.CreateFailure("Download failed"));

        // Act
        await _viewModel.InstallReleaseAssetAsync("owner", "repo", asset);

        // Assert
        Assert.Contains("Download failed", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsInstalling);
    }

    /// <summary>
    /// Verifies that CancelInstallation stops installation and logs the event.
    /// </summary>
    [Fact]
    public void CancelInstallation_StopsInstallationAndLogs()
    {
        _viewModel.IsInstalling = true;
        _viewModel.InstallationProgress = 50;

        // Act
        _viewModel.CancelInstallation();

        // Assert
        Assert.Equal("Installation cancelled", _viewModel.StatusMessage);
        Assert.False(_viewModel.IsInstalling);
        Assert.Equal(0, _viewModel.InstallationProgress);
        Assert.Contains("Installation cancelled", _viewModel.InstallationLog);
    }
}
