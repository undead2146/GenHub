using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Services;

/// <summary>
/// Tests for GitHubServiceFacade.
/// </summary>
public class GitHubServiceFacadeTests
{
    private readonly Mock<IGitHubApiClient> _apiClientMock;
    private readonly Mock<ILogger<GitHubServiceFacade>> _loggerMock;
    private readonly GitHubServiceFacade _facade;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubServiceFacadeTests"/> class.
    /// </summary>
    public GitHubServiceFacadeTests()
    {
        _apiClientMock = new Mock<IGitHubApiClient>();
        _loggerMock = new Mock<ILogger<GitHubServiceFacade>>();
        _facade = new GitHubServiceFacade(_apiClientMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that GetRepositoryReleasesAsync returns releases successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryReleasesAsync_ReturnsReleasesSuccessfully()
    {
        // Arrange
        var releases = new List<GitHubRelease>
        {
            new GitHubRelease { TagName = "v1.0.0", Name = "Release 1" },
            new GitHubRelease { TagName = "v2.0.0", Name = "Release 2" },
        };
        _apiClientMock.Setup(x => x.GetReleasesAsync("owner", "repo", default))
            .ReturnsAsync(releases);

        // Act
        var result = await _facade.GetRepositoryReleasesAsync("owner", "repo");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("v1.0.0", result.Data[0].TagName);
        _apiClientMock.Verify(x => x.GetReleasesAsync("owner", "repo", default), Times.Once);
    }

    /// <summary>
    /// Verifies that GetRepositoryWorkflowsAsync returns empty list (placeholder implementation).
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryWorkflowsAsync_ReturnsEmptyList()
    {
        // Act
        var result = await _facade.GetRepositoryWorkflowsAsync("owner", "repo");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    /// <summary>
    /// Verifies that GetRepositoryWorkflowRunsAsync returns workflow runs successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryWorkflowRunsAsync_ReturnsWorkflowRunsSuccessfully()
    {
        // Arrange
        var workflowRuns = new List<GitHubWorkflowRun>
        {
            new GitHubWorkflowRun { Id = 1, Workflow = new GitHubWorkflow { Name = "CI Run 1" } },
            new GitHubWorkflowRun { Id = 2, Workflow = new GitHubWorkflow { Name = "CI Run 2" } },
        };
        _apiClientMock.Setup(x => x.GetWorkflowRunsForRepositoryAsync("owner", "repo", 10, default))
            .ReturnsAsync(workflowRuns);

        // Act
        var result = await _facade.GetRepositoryWorkflowRunsAsync("owner", "repo");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(1L, result.Data[0].Id);
        _apiClientMock.Verify(x => x.GetWorkflowRunsForRepositoryAsync("owner", "repo", 10, default), Times.Once);
    }

    /// <summary>
    /// Verifies that GetWorkflowRunArtifactsAsync returns artifacts successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetWorkflowRunArtifactsAsync_ReturnsArtifactsSuccessfully()
    {
        // Arrange
        var artifacts = new List<GitHubArtifact>
        {
            new GitHubArtifact { Id = 1, Name = "artifact1.zip" },
            new GitHubArtifact { Id = 2, Name = "artifact2.zip" },
        };
        _apiClientMock.Setup(x => x.GetArtifactsForWorkflowRunAsync("owner", "repo", 123, default))
            .ReturnsAsync(artifacts);

        // Act
        var result = await _facade.GetWorkflowRunArtifactsAsync("owner", "repo", 123);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(1L, result.Data[0].Id);
        _apiClientMock.Verify(x => x.GetArtifactsForWorkflowRunAsync("owner", "repo", 123, default), Times.Once);
    }

    /// <summary>
    /// Verifies that DownloadArtifactAsync downloads artifact successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DownloadArtifactAsync_DownloadsArtifactSuccessfully()
    {
        // Arrange
        var artifact = new GitHubArtifact { Id = 1, Name = "test.zip" };
        var destinationPath = "C:\\Downloads\\test.zip";
        _apiClientMock.Setup(x => x.DownloadArtifactAsync("owner", "repo", artifact, destinationPath, default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _facade.DownloadArtifactAsync("owner", "repo", artifact, destinationPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(destinationPath, ((DownloadResult)result).FilePath);
        _apiClientMock.Verify(x => x.DownloadArtifactAsync("owner", "repo", artifact, destinationPath, default), Times.Once);
    }

    /// <summary>
    /// Verifies that DownloadReleaseAssetAsync downloads release asset successfully.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DownloadReleaseAssetAsync_DownloadsReleaseAssetSuccessfully()
    {
        // Arrange
        var asset = new GitHubReleaseAsset { Id = 1, Name = "release.zip" };
        var destinationPath = "C:\\Downloads\\release.zip";
        _apiClientMock.Setup(x => x.DownloadReleaseAssetAsync("owner", "repo", asset, destinationPath, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _facade.DownloadReleaseAssetAsync("owner", "repo", asset, destinationPath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(destinationPath, result.FilePath);
        _apiClientMock.Verify(x => x.DownloadReleaseAssetAsync("owner", "repo", asset, destinationPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that GetRepositoryReleasesAsync handles exceptions properly.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryReleasesAsync_HandlesExceptionsProperly()
    {
        // Arrange
        _apiClientMock.Setup(x => x.GetReleasesAsync("owner", "repo", default))
            .ThrowsAsync(new System.Exception("API Error"));

        // Act
        var result = await _facade.GetRepositoryReleasesAsync("owner", "repo");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to get releases", result.FirstError);
    }

    /// <summary>
    /// Verifies that GetRepositoryReleasesAsync with valid data returns success.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryReleasesAsync_WithValidData_ReturnsSuccess()
    {
        var releases = new List<GitHubRelease> { new GitHubRelease { Id = 1, TagName = "v1.0" } };
        _apiClientMock.Setup(x => x.GetReleasesAsync("owner", "repo", default))
            .ReturnsAsync(releases);

        var result = await _facade.GetRepositoryReleasesAsync("owner", "repo");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("v1.0", result.Data[0].TagName);
    }

    /// <summary>
    /// Verifies that GetRepositoryWorkflowRunsAsync with valid data returns success.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryWorkflowRunsAsync_WithValidData_ReturnsSuccess()
    {
        var runs = new List<GitHubWorkflowRun> { new GitHubWorkflowRun { Id = 1, RunNumber = 1 } };
        _apiClientMock.Setup(x => x.GetWorkflowRunsForRepositoryAsync("owner", "repo", 10, default))
            .ReturnsAsync(runs);

        var result = await _facade.GetRepositoryWorkflowRunsAsync("owner", "repo");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(1, result.Data[0].RunNumber);
    }

    /// <summary>
    /// Verifies that DownloadArtifactAsync with valid artifact returns success.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task DownloadArtifactAsync_WithValidArtifact_ReturnsSuccess()
    {
        var artifact = new GitHubArtifact { Id = 1, Name = "test" };
        _apiClientMock.Setup(x => x.DownloadArtifactAsync("owner", "repo", artifact, "path", default))
            .Returns(Task.CompletedTask);

        var result = await _facade.DownloadArtifactAsync("owner", "repo", artifact, "path");

        Assert.True(result.Success);
        Assert.Equal("path", result.FilePath);
    }

    /// <summary>
    /// Verifies that GetRepositoryReleasesAsync with exception returns failure.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetRepositoryReleasesAsync_WithException_ReturnsFailure()
    {
        _apiClientMock.Setup(x => x.GetReleasesAsync("owner", "repo", default))
            .ThrowsAsync(new System.Exception("API Error"));

        var result = await _facade.GetRepositoryReleasesAsync("owner", "repo");

        Assert.False(result.Success);
        Assert.Contains("Failed to get releases", result.FirstError);
    }
}
