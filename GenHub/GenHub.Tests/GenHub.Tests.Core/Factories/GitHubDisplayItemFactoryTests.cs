using System.Collections.Generic;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.Factories;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Factories;

/// <summary>
/// Tests for GitHubDisplayItemFactory.
/// </summary>
public class GitHubDisplayItemFactoryTests
{
    private readonly Mock<IGitHubServiceFacade> _serviceMock;
    private readonly Mock<ILogger<GitHubDisplayItemFactory>> _loggerMock;
    private readonly GitHubDisplayItemFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubDisplayItemFactoryTests"/> class.
    /// </summary>
    public GitHubDisplayItemFactoryTests()
    {
        _serviceMock = new Mock<IGitHubServiceFacade>();
        _loggerMock = new Mock<ILogger<GitHubDisplayItemFactory>>();
        _factory = new GitHubDisplayItemFactory(_serviceMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Tests creating display items from valid artifacts.
    /// </summary>
    [Fact]
    public void CreateFromArtifacts_WithValidArtifacts_ReturnsItems()
    {
        var artifacts = new List<GitHubArtifact>
        {
            new GitHubArtifact { Id = 1, Name = "artifact1" },
        };

        var items = _factory.CreateFromArtifacts(artifacts, "owner", "repo");

        Assert.Single(items);
        Assert.Equal("artifact1", items.ToList()[0].DisplayName);
    }

    /// <summary>
    /// Tests creating display items from valid releases.
    /// </summary>
    [Fact]
    public void CreateFromReleases_WithValidReleases_ReturnsItems()
    {
        var releases = new List<GitHubRelease>
        {
            new GitHubRelease { Id = 1, TagName = "v1.0", Name = "Release 1" },
        };

        var items = _factory.CreateFromReleases(releases, "owner", "repo");

        Assert.Single(items);
        Assert.Equal("v1.0", items.ToList()[0].DisplayName);
    }

    /// <summary>
    /// Tests creating display items from valid workflow runs.
    /// </summary>
    [Fact]
    public void CreateFromWorkflowRuns_WithValidRuns_ReturnsItems()
    {
        var runs = new List<GitHubWorkflowRun>
        {
            new GitHubWorkflowRun { Id = 1, RunNumber = 1, Workflow = new GitHubWorkflow { Name = "CI" } },
        };

        var items = _factory.CreateFromWorkflowRuns(runs, "owner", "repo");

        Assert.Single(items);
        Assert.Equal("CI", items.ToList()[0].DisplayName);
    }

    /// <summary>
    /// Tests creating display items from null artifacts.
    /// </summary>
    [Fact]
    public void CreateFromArtifacts_WithNull_ReturnsEmpty()
    {
        var items = _factory.CreateFromArtifacts(null!, "owner", "repo");

        Assert.Empty(items);
    }
}
