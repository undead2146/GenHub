using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Features.GitHub.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;
using Xunit;

namespace GenHub.Tests.Core.Features.AppUpdate.Services;

/// <summary>
/// Tests for OctokitGitHubApiClient.
/// </summary>
public class OctokitGitHubApiClientTests
{
    /// <summary>
    /// Verifies that GetLatestReleaseAsync returns null when not found.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetLatestReleaseAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var releasesClientMock = new Mock<Octokit.IReleasesClient>();
        releasesClientMock
            .Setup(x => x.GetLatest(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException("Not found", HttpStatusCode.NotFound));

        var repositoriesClientMock = new Mock<Octokit.IRepositoriesClient>();
        repositoriesClientMock
            .SetupGet(x => x.Release)
            .Returns(releasesClientMock.Object);

        var gitHubClientMock = new Mock<Octokit.IGitHubClient>();
        gitHubClientMock.SetupGet(x => x.Repository).Returns(repositoriesClientMock.Object);

        var api = new OctokitGitHubApiClient(gitHubClientMock.Object, Mock.Of<IHttpClientFactory>(), Mock.Of<ILogger<OctokitGitHubApiClient>>());

        // Act
        var result = await api.GetLatestReleaseAsync("owner", "repo");

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that GetReleasesAsync returns empty collection when no releases exist.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetReleasesAsync_ReturnsEmptyCollectionWhenNoReleases()
    {
        // Arrange
        var releasesClientMock = new Mock<Octokit.IReleasesClient>();
        releasesClientMock
            .Setup(x => x.GetAll(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Octokit.Release>());

        var repositoriesClientMock = new Mock<Octokit.IRepositoriesClient>();
        repositoriesClientMock
            .SetupGet(x => x.Release)
            .Returns(releasesClientMock.Object);

        var gitHubClientMock = new Mock<Octokit.IGitHubClient>();
        gitHubClientMock.SetupGet(x => x.Repository).Returns(repositoriesClientMock.Object);

        var api = new OctokitGitHubApiClient(gitHubClientMock.Object, Mock.Of<IHttpClientFactory>(), Mock.Of<ILogger<OctokitGitHubApiClient>>());

        // Act
        var result = await api.GetReleasesAsync("owner", "repo");

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that SetAuthenticationToken works with concrete GitHubClient.
    /// </summary>
    [Fact]
    public void SetAuthenticationToken_WorksWithConcreteClient()
    {
        // Arrange
        var concreteClient = new GitHubClient(new ProductHeaderValue("test"));
        var api = new OctokitGitHubApiClient(concreteClient, Mock.Of<IHttpClientFactory>(), Mock.Of<ILogger<OctokitGitHubApiClient>>());
        var secureToken = new SecureString();
        foreach (char c in "test-token")
        {
            secureToken.AppendChar(c);
        }

        // Act & Assert
        api.SetAuthenticationToken(secureToken);
        concreteClient.Credentials.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that SetAuthenticationToken throws when client doesn't support credentials.
    /// </summary>
    [Fact]
    public void SetAuthenticationToken_ThrowsWithMockClient()
    {
        // Arrange
        var mockClient = new Mock<Octokit.IGitHubClient>();
        var api = new OctokitGitHubApiClient(mockClient.Object, Mock.Of<IHttpClientFactory>(), Mock.Of<ILogger<OctokitGitHubApiClient>>());
        var secureToken = new SecureString();
        foreach (char c in "test-token")
        {
            secureToken.AppendChar(c);
        }

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => api.SetAuthenticationToken(secureToken));
    }
}
