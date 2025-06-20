using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GitHub.Services
{
    /// <summary>
    /// Additional tests to ensure strict compliance with requirements.md
    /// </summary>
    public class GitHubRepositoryDiscoveryServiceRequirementsTests
    {
        private readonly Mock<ILogger<GitHubRepositoryDiscoveryService>> _mockLogger;
        private readonly Mock<IGitHubApiClient> _mockApiClient;
        private readonly Mock<IGitHubRepositoryManager> _mockRepositoryManager;
        private readonly GitHubRepositoryDiscoveryService _service;

        public GitHubRepositoryDiscoveryServiceRequirementsTests()
        {
            _mockLogger = new Mock<ILogger<GitHubRepositoryDiscoveryService>>();
            _mockApiClient = new Mock<IGitHubApiClient>();
            _mockRepositoryManager = new Mock<IGitHubRepositoryManager>();
            
            _service = new GitHubRepositoryDiscoveryService(
                _mockLogger.Object,
                _mockApiClient.Object,
                _mockRepositoryManager.Object);
        }

        #region Requirements.md Compliance Tests

        [Fact]
        public async Task DiscoverRepositoriesAsync_RejectsRepositoryWithDraftReleases()
        {
            // Arrange - Repository with only draft releases (should be rejected per requirements.md)
            var repo = CreateTestRepository("user", "test-repo");
            var draftReleases = new[]
            {
                new GitHubRelease
                {
                    TagName = "v1.0",
                    Draft = true, // Draft releases DO NOT count
                    Assets = new List<GitHubReleaseAsset> { new GitHubReleaseAsset { Name = "asset.zip" } }
                }
            };

            SetupApiClient(repo, draftReleases, null);
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().NotContain(r => r.RepoOwner == repo.RepoOwner && r.RepoName == repo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_RejectsRepositoryWithEmptyReleases()
        {
            // Arrange - Repository with releases but no assets (should be rejected per requirements.md)
            var repo = CreateTestRepository("user", "test-repo");
            var emptyReleases = new[]
            {
                new GitHubRelease
                {
                    TagName = "v1.0",
                    Draft = false,
                    Assets = new List<GitHubReleaseAsset>() // Empty releases without assets DO NOT count
                }
            };

            SetupApiClient(repo, emptyReleases, null);
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().NotContain(r => r.RepoOwner == repo.RepoOwner && r.RepoName == repo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_RejectsRepositoryWithFailedWorkflows()
        {
            // Arrange - Repository with only failed workflows (should be rejected per requirements.md)
            var repo = CreateTestRepository("user", "test-repo");
            var failedWorkflows = new[]
            {
                new GitHubWorkflow
                {
                    Id = 1,
                    Status = "completed",
                    Conclusion = "failure" // Failed workflows DO NOT count
                }
            };

            SetupApiClient(repo, null, failedWorkflows);
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().NotContain(r => r.RepoOwner == repo.RepoOwner && r.RepoName == repo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_AcceptsRepositoryWithValidReleases()
        {
            // Arrange - Repository with published releases that have assets (should be accepted)
            var repo = CreateTestRepository("user", "test-repo");
            var validReleases = new[]
            {
                new GitHubRelease
                {
                    TagName = "v1.0",
                    Draft = false, // Published release
                    Assets = new List<GitHubReleaseAsset> 
                    { 
                        new GitHubReleaseAsset { Name = "asset.zip" } // Has downloadable assets
                    }
                }
            };

            SetupApiClient(repo, validReleases, null);
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().Contain(r => r.RepoOwner == repo.RepoOwner && r.RepoName == repo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_AcceptsRepositoryWithSuccessfulWorkflows()
        {
            // Arrange - Repository with successful workflows (should be accepted)
            var repo = CreateTestRepository("user", "test-repo");
            var successfulWorkflows = new[]
            {
                new GitHubWorkflow
                {
                    Id = 1,
                    Status = "completed",
                    Conclusion = "success" // Successful workflow
                }
            };

            SetupApiClient(repo, null, successfulWorkflows);
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().Contain(r => r.RepoOwner == repo.RepoOwner && r.RepoName == repo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_RejectsRepositoryWithNoReleasesAndNoWorkflows()
        {
            // Arrange - Repository with NO releases AND NO workflows (should be strictly rejected)
            var repo = CreateTestRepository("user", "test-repo");

            SetupApiClient(repo, Enumerable.Empty<GitHubRelease>(), Enumerable.Empty<GitHubWorkflow>());
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().NotContain(r => r.RepoOwner == repo.RepoOwner && r.RepoName == repo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_AcceptsMarkerRepositories()
        {
            // Arrange - Marker repositories should always be accepted (for validation)
            var markerRepo = CreateTestRepository("jmarshall2323", "CnC_Generals_Zero_Hour"); // Known marker

            SetupApiClient(markerRepo, Enumerable.Empty<GitHubRelease>(), Enumerable.Empty<GitHubWorkflow>());
            SetupRepositoryManager();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var discoveredRepos = result.Data?.ToList() ?? new List<GitHubRepository>();
            discoveredRepos.Should().Contain(r => r.RepoOwner == markerRepo.RepoOwner && r.RepoName == markerRepo.RepoName);
        }

        #endregion

        #region Helper Methods

        private void SetupApiClient(GitHubRepository repo, IEnumerable<GitHubRelease>? releases, IEnumerable<GitHubWorkflow>? workflows)
        {
            // Setup base repository access
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { repo });

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRepository>());

            // Setup search to return the test repo
            _mockApiClient
                .Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { repo });

            // Setup releases
            _mockApiClient
                .Setup(x => x.GetReleasesForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(releases ?? Enumerable.Empty<GitHubRelease>());

            // Setup workflows
            _mockApiClient
                .Setup(x => x.GetWorkflowRunsForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(workflows ?? Enumerable.Empty<GitHubWorkflow>());
        }

        private void SetupRepositoryManager()
        {
            _mockRepositoryManager
                .Setup(x => x.GetRepositoriesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRepository>());

            _mockRepositoryManager
                .Setup(x => x.AddOrUpdateRepositoriesAsync(It.IsAny<IEnumerable<GitHubRepository>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Succeeded());
        }

        private static GitHubRepository CreateTestRepository(string owner, string name)
        {
            return new GitHubRepository
            {
                RepoOwner = owner,
                RepoName = name,
                Description = "Command & Conquer Generals modification",
                StargazersCount = 1,
                ForksCount = 0,
                WatchersCount = 0,
                OpenIssuesCount = 0,
                Size = 1000,
                Language = "C++",
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                PushedAt = DateTime.UtcNow.AddDays(-1),
                IsPrivate = false,
                IsArchived = false,
                IsDisabled = false,
                IsFork = true,
                HasIssues = true,
                HasProjects = false,
                HasWiki = false,
                DefaultBranch = "master",
                Topics = new[] { "generals", "command-conquer" },
                License = "MIT",
                DisplayName = $"{owner}/{name}",
                Branch = "master",
                Enabled = true,
                LastAccessed = DateTime.UtcNow
            };
        }

        #endregion
    }
}
