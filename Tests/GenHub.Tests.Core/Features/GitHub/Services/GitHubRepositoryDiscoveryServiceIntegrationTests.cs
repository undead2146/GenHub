using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GitHub.Services
{
    /// <summary>
    /// Integration tests for GitHubRepositoryDiscoveryService focusing on real-world scenarios
    /// </summary>
    public class GitHubRepositoryDiscoveryServiceIntegrationTests
    {
        private readonly Mock<ILogger<GitHubRepositoryDiscoveryService>> _mockLogger;
        private readonly Mock<IGitHubApiClient> _mockApiClient;
        private readonly Mock<IGitHubRepositoryManager> _mockRepositoryManager;
        private readonly GitHubRepositoryDiscoveryService _service;

        public GitHubRepositoryDiscoveryServiceIntegrationTests()
        {
            _mockLogger = new Mock<ILogger<GitHubRepositoryDiscoveryService>>();
            _mockApiClient = new Mock<IGitHubApiClient>();
            _mockRepositoryManager = new Mock<IGitHubRepositoryManager>();
            
            _service = new GitHubRepositoryDiscoveryService(
                _mockLogger.Object,
                _mockApiClient.Object,
                _mockRepositoryManager.Object);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_CompleteWorkflow_FindsAllMarkerRepositories()
        {
            // Arrange
            SetupCompleteDiscoveryScenario();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(
                new DiscoveryOptions { MaxResultsToReturn = 100 }, 
                CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var repositories = result.Data.ToList();
            repositories.Should().NotBeEmpty();

            // Verify all marker repositories are found
            var markerRepositories = GetExpectedMarkerRepositories();
            foreach (var marker in markerRepositories)
            {
                repositories.Should().Contain(r => 
                    r.RepoOwner.Equals(marker.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                    r.RepoName.Equals(marker.RepoName, StringComparison.OrdinalIgnoreCase),
                    $"Marker repository {marker.RepoOwner}/{marker.RepoName} should be found");
            }

            // Verify API calls were made efficiently
            VerifyApiCallsWereMade();
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithRateLimiting_RespectsDelays()
        {
            // Arrange
            var callTimes = new List<DateTime>();
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    callTimes.Add(DateTime.UtcNow);
                    return Task.FromResult(Enumerable.Empty<GitHubRepository>());
                });

            SetupBaseRepositoriesForRateLimitTest();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(
                new DiscoveryOptions { RateLimitDelayMs = 100 }, 
                CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            
            // Verify rate limiting was applied (allowing some variance for test execution)
            if (callTimes.Count > 1)
            {
                for (int i = 1; i < callTimes.Count; i++)
                {
                    var timeDiff = callTimes[i] - callTimes[i - 1];
                    timeDiff.TotalMilliseconds.Should().BeGreaterThan(50, 
                        "Rate limiting should enforce delays between API calls");
                }
            }
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithLargeNumberOfForks_HandlesEfficientlyWithTimeout()
        {
            // Arrange
            var largeNumberOfForks = CreateLargeNumberOfTestForks(200);
            SetupScenarioWithManyForks(largeNumberOfForks);

            var options = new DiscoveryOptions 
            { 
                MaxForksPerRepository = 50,
                MaxForksToEvaluate = 100,
                RequestTimeoutSeconds = 30
            };

            // Act
            var result = await _service.DiscoverRepositoriesAsync(options, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            
            // Should handle large number of forks without timing out
            var repositories = result.Data.ToList();
            repositories.Should().NotBeEmpty();
            
            // Should respect the fork limits
            repositories.Count.Should().BeLessOrEqualTo(options.MaxForksToEvaluate + 10); // Allow some buffer for base repos
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithNetworkErrors_HandlesGracefully()
        {
            // Arrange
            var callCount = 0;
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount <= 2)
                    {
                        throw new Exception("Network error");
                    }
                    return Task.FromResult(CreateValidTestRepository("recovered", "repo"));
                });

            SetupBaseRepositoriesForErrorHandling();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            
            // Should still find repositories despite some network errors
            var repositories = result.Data.ToList();
            repositories.Should().NotBeEmpty();
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithFilteringScenario_FiltersCorrectly()
        {
            // Arrange
            var mixedRepositories = CreateMixedQualityRepositories();
            SetupScenarioWithMixedRepositories(mixedRepositories);

            var options = new DiscoveryOptions 
            { 
                RequireActionableContent = true,
                MinimumActivityScore = 10,
                MaxMonthsSinceLastPush = 12
            };

            // Act
            var result = await _service.DiscoverRepositoriesAsync(options, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var repositories = result.Data.ToList();
            
            // Should only include high-quality repositories
            repositories.Should().NotBeEmpty();
            repositories.Should().OnlyContain(r => 
                r.PushedAt.HasValue && r.PushedAt.Value > DateTime.UtcNow.AddMonths(-12),
                "All repositories should have recent activity");
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithDeepNetworkTraversal_FindsNestedForks()
        {
            // Arrange
            SetupDeepNetworkTraversalScenario();

            var options = new DiscoveryOptions 
            { 
                MaxForkDepth = 3,
                MaxForksPerRepository = 20
            };

            // Act
            var result = await _service.DiscoverRepositoriesAsync(options, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var repositories = result.Data.ToList();
            
            // Should find repositories at multiple levels
            repositories.Should().NotBeEmpty();
            repositories.Should().HaveCountGreaterThan(5, "Should find repositories through deep traversal");
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithCancellation_StopsGracefully()
        {
            // Arrange
            using var cancellationTokenSource = new CancellationTokenSource();
            
            var delayCount = 0;
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    delayCount++;
                    if (delayCount == 2)
                    {
                        cancellationTokenSource.Cancel();
                    }
                    await Task.Delay(100);
                    return CreateTestForks(5, "test");
                });

            SetupBaseRepositoriesForCancellationTest();

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), cancellationTokenSource.Token));
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithSearchExpansion_FindsAdditionalRepositories()
        {
            // Arrange
            SetupSearchExpansionScenario();

            var options = new DiscoveryOptions 
            { 
                IncludeSearch = true,
                MaxSearchResults = 20,
                MaxSearchQueries = 5
            };

            // Act
            var result = await _service.DiscoverRepositoriesAsync(options, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var repositories = result.Data.ToList();
            
            // Should find repositories through search in addition to fork traversal
            repositories.Should().NotBeEmpty();
            
            // Verify search was called
            _mockApiClient.Verify(
                x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        #region Helper Methods

        private void SetupCompleteDiscoveryScenario()
        {
            // Setup base repositories
            var baseRepositories = GetBaseRepositories();
            foreach (var repo in baseRepositories)
            {
                _mockApiClient
                    .Setup(x => x.GetRepositoryInfoAsync(repo.RepoOwner, repo.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(repo);
                
                // Setup forks for each base repository
                var forks = CreateForksForRepository(repo);
                _mockApiClient
                    .Setup(x => x.GetRepositoryForksAsync(repo.RepoOwner, repo.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(forks);
            }

            // Setup marker repositories in forks
            var markerRepositories = GetExpectedMarkerRepositories();
            foreach (var marker in markerRepositories)
            {
                SetupValidRepositoryMock(marker);
            }

            // Setup repository manager
            _mockRepositoryManager
                .Setup(x => x.GetRepositories())
                .Returns(new List<GitHubRepository>());
        }

        private void SetupBaseRepositoriesForRateLimitTest()
        {
            var baseRepositories = GetBaseRepositories().Take(3).ToList();
            foreach (var repo in baseRepositories)
            {
                _mockApiClient
                    .Setup(x => x.GetRepositoryInfoAsync(repo.RepoOwner, repo.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(repo);
            }
        }

        private void SetupScenarioWithManyForks(IEnumerable<GitHubRepository> forks)
        {
            var baseRepo = GetBaseRepositories().First();
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseRepo);

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(forks);

            // Setup validation for repositories
            foreach (var fork in forks.Take(50)) // Limit to prevent excessive setup
            {
                SetupValidRepositoryMock(fork);
            }
        }

        private void SetupBaseRepositoriesForErrorHandling()
        {
            var baseRepositories = GetBaseRepositories();
            foreach (var repo in baseRepositories)
            {
                _mockApiClient
                    .Setup(x => x.GetRepositoryForksAsync(repo.RepoOwner, repo.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Enumerable.Empty<GitHubRepository>());
            }
        }

        private void SetupScenarioWithMixedRepositories(IEnumerable<GitHubRepository> repositories)
        {
            var baseRepo = GetBaseRepositories().First();
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseRepo);

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repositories);

            // Setup validation based on repository quality
            foreach (var repo in repositories)
            {
                var hasRecentActivity = repo.PushedAt.HasValue && repo.PushedAt.Value > DateTime.UtcNow.AddMonths(-12);
                var hasContent = repo.StargazersCount > 0 || repo.ForksCount > 0;
                
                if (hasRecentActivity && hasContent)
                {
                    SetupValidRepositoryMock(repo);
                }
                else
                {
                    SetupInvalidRepositoryMock(repo);
                }
            }
        }

        private void SetupDeepNetworkTraversalScenario()
        {
            var baseRepo = GetBaseRepositories().First();
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseRepo);

            // Level 1: Base repository forks
            var level1Forks = CreateTestForks(5, "level1");
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(level1Forks);

            // Level 2: Forks of forks
            foreach (var fork in level1Forks)
            {
                var level2Forks = CreateTestForks(3, $"level2-{fork.RepoOwner}");
                _mockApiClient
                    .Setup(x => x.GetRepositoryForksAsync(fork.RepoOwner, fork.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(level2Forks);

                SetupValidRepositoryMock(fork);
                
                foreach (var level2Fork in level2Forks)
                {
                    SetupValidRepositoryMock(level2Fork);
                }
            }
        }

        private void SetupBaseRepositoriesForCancellationTest()
        {
            var baseRepositories = GetBaseRepositories().Take(3).ToList();
            foreach (var repo in baseRepositories)
            {
                _mockApiClient
                    .Setup(x => x.GetRepositoryInfoAsync(repo.RepoOwner, repo.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(repo);
            }
        }

        private void SetupSearchExpansionScenario()
        {
            // Setup base repositories
            var baseRepo = GetBaseRepositories().First();
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseRepo);

            // Setup empty forks for base repositories
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRepository>());

            // Setup search results
            var searchResults = CreateTestForks(10, "search");
            _mockApiClient
                .Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            foreach (var repo in searchResults)
            {
                SetupValidRepositoryMock(repo);
            }
        }

        private void SetupValidRepositoryMock(GitHubRepository repo)
        {
            _mockApiClient
                .Setup(x => x.GetReleasesForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new GitHubRelease
                    {
                        TagName = "v1.0",
                        Assets = new List<GitHubReleaseAsset> { new GitHubReleaseAsset { Name = "asset.zip" } }
                    }
                });
        }

        private void SetupInvalidRepositoryMock(GitHubRepository repo)
        {
            _mockApiClient
                .Setup(x => x.GetReleasesForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRelease>());

            _mockApiClient
                .Setup(x => x.GetWorkflowRunsForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubWorkflow>());
        }

        private void VerifyApiCallsWereMade()
        {
            // Verify that base repository calls were made
            _mockApiClient.Verify(
                x => x.GetRepositoryInfoAsync("TheAssemblyArmada", "Vanilla-Conquer", It.IsAny<CancellationToken>()),
                Times.Once);
            
            _mockApiClient.Verify(
                x => x.GetRepositoryInfoAsync("TheSuperHackers", "GeneralsGamePatch", It.IsAny<CancellationToken>()),
                Times.Once);
            
            _mockApiClient.Verify(
                x => x.GetRepositoryInfoAsync("xezon", "CnC_GeneralsGameCode", It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify fork discovery calls were made
            _mockApiClient.Verify(
                x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        private static IEnumerable<GitHubRepository> GetBaseRepositories()
        {
            return new[]
            {
                CreateValidTestRepository("TheAssemblyArmada", "Vanilla-Conquer"),
                CreateValidTestRepository("TheSuperHackers", "GeneralsGamePatch"),
                CreateValidTestRepository("xezon", "CnC_GeneralsGameCode")
            };
        }

        private static IEnumerable<GitHubRepository> GetExpectedMarkerRepositories()
        {
            return new[]
            {
                CreateValidTestRepository("DooMLoRD", "Command-and-Conquer-Generals-ZeroHour-Linux"),
                CreateValidTestRepository("TheFixer", "generals-enhanced"),
                CreateValidTestRepository("Commoble", "generals-dataviewer")
            };
        }

        private static IEnumerable<GitHubRepository> CreateForksForRepository(GitHubRepository baseRepo)
        {
            var forks = new List<GitHubRepository>();
            
            // Add some marker repositories as forks
            if (baseRepo.RepoOwner == "TheAssemblyArmada")
            {
                forks.AddRange(GetExpectedMarkerRepositories());
            }
            
            // Add some regular forks
            forks.AddRange(CreateTestForks(5, $"fork-of-{baseRepo.RepoName}"));
            
            return forks;
        }

        private static IEnumerable<GitHubRepository> CreateLargeNumberOfTestForks(int count)
        {
            var forks = new List<GitHubRepository>();
            for (int i = 0; i < count; i++)
            {
                forks.Add(CreateValidTestRepository($"user{i}", $"generals-fork-{i}"));
            }
            return forks;
        }

        private static IEnumerable<GitHubRepository> CreateMixedQualityRepositories()
        {
            return new[]
            {
                // High quality - recent activity, has stars
                CreateTestRepository("active-dev", "great-generals-mod", 
                    starCount: 25, pushDate: DateTime.UtcNow.AddDays(-10)),
                
                // Medium quality - older but has community
                CreateTestRepository("community-dev", "popular-generals-mod", 
                    starCount: 10, pushDate: DateTime.UtcNow.AddMonths(-6)),
                
                // Low quality - very old, no community
                CreateTestRepository("old-dev", "abandoned-generals-mod", 
                    starCount: 0, pushDate: DateTime.UtcNow.AddYears(-2)),
                
                // High quality - recent, good community
                CreateTestRepository("new-dev", "fresh-generals-mod", 
                    starCount: 15, pushDate: DateTime.UtcNow.AddDays(-5))
            };
        }

        private static IEnumerable<GitHubRepository> CreateTestForks(int count, string prefix)
        {
            var forks = new List<GitHubRepository>();
            for (int i = 0; i < count; i++)
            {
                forks.Add(CreateValidTestRepository($"{prefix}-user{i}", $"{prefix}-generals-repo-{i}"));
            }
            return forks;
        }

        private static GitHubRepository CreateValidTestRepository(string owner, string name)
        {
            return CreateTestRepository(owner, name, starCount: 5, pushDate: DateTime.UtcNow.AddDays(-30));
        }

        private static GitHubRepository CreateTestRepository(
            string owner,
            string name,
            int starCount = 0,
            DateTime? pushDate = null)
        {
            return new GitHubRepository
            {
                RepoOwner = owner,
                RepoName = name,
                Description = "Command & Conquer Generals modification",
                StargazersCount = starCount,
                ForksCount = 2,
                WatchersCount = 1,
                OpenIssuesCount = 0,
                Size = 1000,
                Language = "C++",
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                PushedAt = pushDate ?? DateTime.UtcNow.AddDays(-1),                IsPrivate = false,
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
