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
    public class GitHubRepositoryDiscoveryServiceTests
    {
        private readonly Mock<ILogger<GitHubRepositoryDiscoveryService>> _mockLogger;
        private readonly Mock<IGitHubApiClient> _mockApiClient;
        private readonly Mock<IGitHubRepositoryManager> _mockRepositoryManager;
        private readonly GitHubRepositoryDiscoveryService _service;

        public GitHubRepositoryDiscoveryServiceTests()
        {
            _mockLogger = new Mock<ILogger<GitHubRepositoryDiscoveryService>>();
            _mockApiClient = new Mock<IGitHubApiClient>();
            _mockRepositoryManager = new Mock<IGitHubRepositoryManager>();
            
            _service = new GitHubRepositoryDiscoveryService(
                _mockLogger.Object,
                _mockApiClient.Object,
                _mockRepositoryManager.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new GitHubRepositoryDiscoveryService(
                    null!,
                    _mockApiClient.Object,
                    _mockRepositoryManager.Object));
            
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullApiClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubRepositoryDiscoveryService(
                    _mockLogger.Object,
                    null!,
                    _mockRepositoryManager.Object));
            
            exception.ParamName.Should().Be("apiClient");
        }

        [Fact]
        public void Constructor_WithNullRepositoryManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubRepositoryDiscoveryService(
                    _mockLogger.Object,
                    _mockApiClient.Object,
                    null!));
            
            exception.ParamName.Should().Be("repositoryManager");
        }

        #endregion

        #region DiscoverRepositoriesAsync Tests

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithValidOptions_ReturnsSuccessResult()
        {
            // Arrange
            var options = new DiscoveryOptions { MaxResultsToReturn = 10 };
            SetupMockForSuccessfulDiscovery();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(options, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithNullOptions_UsesDefaultOptions()
        {
            // Arrange
            SetupMockForSuccessfulDiscovery();

            // Act
            var result = await _service.DiscoverRepositoriesAsync(null, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WhenApiClientThrows_ReturnsFailureResult()
        {
            // Arrange
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Error"));

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Discovery failed");
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_IncludesAllBaseRepositories()
        {
            // Arrange
            var baseRepositories = CreateTestBaseRepositories();
            SetupMockForBaseRepositories(baseRepositories);
            
            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeEmpty();
            
            // Verify all base repositories are requested
            foreach (var baseRepo in baseRepositories)
            {
                _mockApiClient.Verify(
                    x => x.GetRepositoryInfoAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()),
                    Times.Once);
            }
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_FindsMarkerRepositories()
        {
            // Arrange
            var markerRepositories = CreateTestMarkerRepositories();
            SetupMockForMarkerRepositories(markerRepositories);

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            
            // Check that marker repositories are found in the results
            var foundRepos = result.Data.ToList();
            foreach (var marker in markerRepositories)
            {
                foundRepos.Should().Contain(r => 
                    r.RepoOwner.Equals(marker.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                    r.RepoName.Equals(marker.RepoName, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_DiscoversForks()
        {
            // Arrange
            var baseRepo = CreateTestRepository("TheAssemblyArmada", "Vanilla-Conquer", isBase: true);
            var forks = CreateTestForks(5);
            
            SetupMockForBaseRepositoryWithForks(baseRepo, forks);

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var foundRepos = result.Data.ToList();
            
            // Should include base repository plus valid forks
            foundRepos.Should().HaveCountGreaterThan(1);
            foundRepos.Should().Contain(r => r.RepoOwner == baseRepo.RepoOwner && r.RepoName == baseRepo.RepoName);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_FiltersInvalidRepositories()
        {
            // Arrange
            var validRepo = CreateTestRepository("user1", "generals-mod", hasValidContent: true);
            var invalidRepo = CreateTestRepository("user2", "invalid-repo", hasValidContent: false);
            
            SetupMockForRepositoryValidation(validRepo, true);
            SetupMockForRepositoryValidation(invalidRepo, false);
            
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { validRepo, invalidRepo });

            // Act
            var result = await _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var foundRepos = result.Data.ToList();
            
            foundRepos.Should().Contain(r => r.RepoOwner == validRepo.RepoOwner);
            foundRepos.Should().NotContain(r => r.RepoOwner == invalidRepo.RepoOwner);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_RespectsCancellationToken()
        {
            // Arrange
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _service.DiscoverRepositoriesAsync(new DiscoveryOptions(), cancellationTokenSource.Token));
        }

        #endregion

        #region DiscoverActiveForks Tests

        [Fact]
        public async Task DiscoverActiveForks_WithValidRepository_ReturnsActiveForks()
        {
            // Arrange
            var owner = "TheAssemblyArmada";
            var name = "Vanilla-Conquer";
            var forks = CreateTestForks(3, activeOnly: true);

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(forks);

            // Act
            var result = await _service.DiscoverActiveForks(owner, name, new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(3);
            result.Data.All(f => f.RepoName.Contains("generals")).Should().BeTrue();
        }

        [Fact]
        public async Task DiscoverActiveForks_WithNoForks_ReturnsEmptyResult()
        {
            // Arrange
            var owner = "owner";
            var name = "repo";

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRepository>());

            // Act
            var result = await _service.DiscoverActiveForks(owner, name, new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task DiscoverActiveForks_WhenApiThrows_ReturnsFailureResult()
        {
            // Arrange
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Error"));

            // Act
            var result = await _service.DiscoverActiveForks("owner", "repo", new DiscoveryOptions(), CancellationToken.None);

            // Assert            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Fork discovery failed");
        }

        [Fact]
        public async Task DiscoverActiveForks_FiltersNonGeneralsRepositories()
        {
            // Arrange
            var owner = "owner";
            var name = "repo";
            var forks = new[]
            {
                CreateTestRepository("user1", "generals-mod", isGeneralsRelated: true),
                CreateTestRepository("user2", "tiberian-sun", isGeneralsRelated: false),
                CreateTestRepository("user3", "zerohour-mod", isGeneralsRelated: true)
            };

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(forks);

            // Act
            var result = await _service.DiscoverActiveForks(owner, name, new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(2);
            result.Data.Should().NotContain(r => r.RepoName.Contains("tiberian"));
        }

        #endregion

        #region FindMostActiveForks Tests

        [Fact]
        public async Task FindMostActiveForks_WithActiveRepositories_ReturnsOrderedByActivity()
        {
            // Arrange
            var repositories = new[]
            {
                CreateTestRepository("user1", "generals-mod-1", starCount: 10, pushDate: DateTime.UtcNow.AddDays(-1)),
                CreateTestRepository("user2", "generals-mod-2", starCount: 5, pushDate: DateTime.UtcNow.AddDays(-30)),
                CreateTestRepository("user3", "generals-mod-3", starCount: 20, pushDate: DateTime.UtcNow.AddDays(-7))
            };

            // Act
            var result = await _service.FindMostActiveForks(repositories, new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            var orderedRepos = result.Data.ToList();
            
            // Should be ordered by stars (descending), then by push date
            orderedRepos[0].StargazersCount.Should().Be(20);
            orderedRepos[1].StargazersCount.Should().Be(10);
            orderedRepos[2].StargazersCount.Should().Be(5);
        }

        [Fact]
        public async Task FindMostActiveForks_FiltersInactiveRepositories()
        {
            // Arrange
            var repositories = new[]
            {
                CreateTestRepository("active-user", "generals-mod", 
                    starCount: 10, pushDate: DateTime.UtcNow.AddDays(-30)),
                CreateTestRepository("inactive-user", "old-generals-mod", 
                    starCount: 0, pushDate: DateTime.UtcNow.AddYears(-3))
            };

            // Act
            var result = await _service.FindMostActiveForks(repositories, new DiscoveryOptions(), CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(1);
            result.Data.First().RepoOwner.Should().Be("active-user");
        }

        [Fact]
        public async Task FindMostActiveForks_RespectsMaxResultsLimit()
        {
            // Arrange
            var repositories = CreateTestForks(30, activeOnly: true);
            var options = new DiscoveryOptions { MaxResultsToReturn = 5 };

            // Act
            var result = await _service.FindMostActiveForks(repositories, options, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(5);
        }

        #endregion

        #region AddDiscoveredRepositoriesAsync Tests

        [Fact]
        public async Task AddDiscoveredRepositoriesAsync_WithNewRepositories_AddsSuccessfully()
        {
            // Arrange
            var newRepositories = CreateTestForks(3);
            var existingRepositories = new List<GitHubRepository>();

            _mockRepositoryManager
                .Setup(x => x.GetRepositories())
                .Returns(existingRepositories);

            _mockRepositoryManager
                .Setup(x => x.SaveRepositories(It.IsAny<List<GitHubRepository>>()))
                .Verifiable();

            // Act
            var result = await _service.AddDiscoveredRepositoriesAsync(newRepositories, false, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            _mockRepositoryManager.Verify(x => x.SaveRepositories(It.Is<List<GitHubRepository>>(
                repos => repos.Count == 3)), Times.Once);
        }

        [Fact]
        public async Task AddDiscoveredRepositoriesAsync_WithExistingRepositories_SkipsWithoutReplace()
        {
            // Arrange
            var repository = CreateTestRepository("user", "repo");
            var repositories = new[] { repository };
            var existingRepositories = new List<GitHubRepository> { repository };

            _mockRepositoryManager
                .Setup(x => x.GetRepositories())
                .Returns(existingRepositories);

            // Act
            var result = await _service.AddDiscoveredRepositoriesAsync(repositories, false, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            _mockRepositoryManager.Verify(x => x.SaveRepositories(It.IsAny<List<GitHubRepository>>()), Times.Never);
        }

        [Fact]
        public async Task AddDiscoveredRepositoriesAsync_WithReplaceExisting_UpdatesRepositories()
        {
            // Arrange
            var existingRepo = CreateTestRepository("user", "repo");
            var updatedRepo = CreateTestRepository("user", "repo");
            updatedRepo.DisplayName = "Updated Display Name";

            var existingRepositories = new List<GitHubRepository> { existingRepo };

            _mockRepositoryManager
                .Setup(x => x.GetRepositories())
                .Returns(existingRepositories);

            _mockRepositoryManager
                .Setup(x => x.SaveRepositories(It.IsAny<List<GitHubRepository>>()))
                .Verifiable();

            // Act
            var result = await _service.AddDiscoveredRepositoriesAsync(new[] { updatedRepo }, true, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            existingRepo.DisplayName.Should().Be("Updated Display Name");
        }

        [Fact]
        public async Task AddDiscoveredRepositoriesAsync_WhenSaveThrows_ReturnsFailureResult()
        {
            // Arrange
            var repositories = CreateTestForks(1);
            _mockRepositoryManager
                .Setup(x => x.GetRepositories())
                .Returns(new List<GitHubRepository>());

            _mockRepositoryManager
                .Setup(x => x.SaveRepositories(It.IsAny<List<GitHubRepository>>()))
                .Throws(new Exception("Save error"));

            // Act
            var result = await _service.AddDiscoveredRepositoriesAsync(repositories, false, CancellationToken.None);

            // Assert            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Failed to add repositories");
        }

        #endregion

        #region Helper Methods

        private void SetupMockForSuccessfulDiscovery()
        {
            var baseRepositories = CreateTestBaseRepositories();
            SetupMockForBaseRepositories(baseRepositories);

            // Setup empty forks for base repositories
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRepository>());

            // Setup empty search results
            _mockApiClient
                .Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<GitHubRepository>());

            // Setup repository manager
            _mockRepositoryManager
                .Setup(x => x.GetRepositories())
                .Returns(new List<GitHubRepository>());
        }

        private void SetupMockForBaseRepositories(IEnumerable<GitHubRepository> baseRepositories)
        {
            foreach (var repo in baseRepositories)
            {
                _mockApiClient
                    .Setup(x => x.GetRepositoryInfoAsync(repo.RepoOwner, repo.RepoName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(repo);
            }
        }

        private void SetupMockForMarkerRepositories(IEnumerable<GitHubRepository> markerRepositories)
        {
            SetupMockForBaseRepositories(markerRepositories);

            // Setup forks for base repositories that include marker repositories
            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(markerRepositories);

            // Setup validation for marker repositories
            foreach (var marker in markerRepositories)
            {
                SetupMockForRepositoryValidation(marker, true);
            }
        }

        private void SetupMockForBaseRepositoryWithForks(GitHubRepository baseRepo, IEnumerable<GitHubRepository> forks)
        {
            _mockApiClient
                .Setup(x => x.GetRepositoryInfoAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseRepo);

            _mockApiClient
                .Setup(x => x.GetRepositoryForksAsync(baseRepo.RepoOwner, baseRepo.RepoName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(forks);

            // Setup validation for all repos
            SetupMockForRepositoryValidation(baseRepo, true);
            foreach (var fork in forks)
            {
                SetupMockForRepositoryValidation(fork, true);
            }
        }

        private void SetupMockForRepositoryValidation(GitHubRepository repo, bool isValid)
        {
            if (isValid)
            {
                // Setup releases
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
            else
            {
                // Setup no releases or workflows
                _mockApiClient
                    .Setup(x => x.GetReleasesForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Enumerable.Empty<GitHubRelease>());                _mockApiClient
                    .Setup(x => x.GetWorkflowRunsForRepositoryAsync(repo.RepoOwner, repo.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Enumerable.Empty<GitHubWorkflow>());
            }
        }

        private static IEnumerable<GitHubRepository> CreateTestBaseRepositories()
        {
            return new[]
            {
                CreateTestRepository("TheAssemblyArmada", "Vanilla-Conquer", isBase: true),
                CreateTestRepository("TheSuperHackers", "GeneralsGamePatch", isBase: true),
                CreateTestRepository("xezon", "CnC_GeneralsGameCode", isBase: true)
            };
        }

        private static IEnumerable<GitHubRepository> CreateTestMarkerRepositories()
        {
            return new[]
            {
                CreateTestRepository("DooMLoRD", "Command-and-Conquer-Generals-ZeroHour-Linux", isGeneralsRelated: true),
                CreateTestRepository("TheFixer", "generals-enhanced", isGeneralsRelated: true),
                CreateTestRepository("Commoble", "generals-dataviewer", isGeneralsRelated: true)
            };
        }

        private static IEnumerable<GitHubRepository> CreateTestForks(int count, bool activeOnly = false)
        {
            var forks = new List<GitHubRepository>();
            for (int i = 0; i < count; i++)
            {
                var repo = CreateTestRepository(
                    $"user{i}",
                    $"generals-fork-{i}",
                    isGeneralsRelated: true,
                    starCount: activeOnly ? i + 1 : 0,
                    pushDate: activeOnly ? DateTime.UtcNow.AddDays(-i) : DateTime.UtcNow.AddYears(-1));
                forks.Add(repo);
            }
            return forks;
        }

        private static GitHubRepository CreateTestRepository(
            string owner,
            string name,
            bool isBase = false,
            bool isGeneralsRelated = true,
            bool hasValidContent = true,
            int starCount = 0,
            DateTime? pushDate = null)
        {
            var description = isGeneralsRelated ? "Command & Conquer Generals modification" : "Some other project";
            
            return new GitHubRepository
            {
                RepoOwner = owner,
                RepoName = name,
                Description = description,
                StargazersCount = starCount,
                ForksCount = 0,
                WatchersCount = 0,
                OpenIssuesCount = 0,
                Size = 1000,
                Language = "C++",
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),                PushedAt = pushDate ?? DateTime.UtcNow.AddDays(-1),
                IsPrivate = false,
                IsArchived = false,
                IsDisabled = false,
                IsFork = !isBase,
                HasIssues = true,
                HasProjects = false,
                HasWiki = false,
                DefaultBranch = "master",
                Topics = isGeneralsRelated ? new[] { "generals", "command-conquer" } : new[] { "other" },
                License = "MIT",
                DisplayName = $"{owner}/{name}",
                Branch = "master",
                Enabled = true,
                LastAccessed = DateTime.UtcNow
            };
        }

        private GitHubRelease CreateTestRelease(string tagName, bool hasAssets = false)
        {
            return new GitHubRelease
            {
                TagName = tagName,
                Assets = hasAssets ? new List<GitHubReleaseAsset> { new GitHubReleaseAsset { Name = "asset.zip" } } : new List<GitHubReleaseAsset>()
            };
        }

        #endregion

        #region Fork Validation Tests

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithNonForkRepository_RejectsRepository()
        {
            // Arrange
            var nonForkRepo = CreateTestRepository("test-owner", "test-repo", isBase: true, isGeneralsRelated: true);
            nonForkRepo.IsFork = false; // Explicitly set to non-fork

            // Mock API to return non-fork in search results
            _mockApiClient.Setup(x => x.SearchRepositoriesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { nonForkRepo });

            // Mock base repository info
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("electronicarts", "CnC_Generals_Zero_Hour", isBase: true));
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("TheSuperHackers", "GeneralsGameCode", isBase: true));

            // Mock empty forks to focus on search results
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRepository>());            // Act
            var result = await _service.DiscoverRepositoriesAsync();

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().BeEmpty("Non-fork repositories should be rejected");
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithValidFork_IncludesRepository()
        {
            // Arrange
            var validFork = CreateTestRepository("fork-owner", "fork-repo", isBase: false, isGeneralsRelated: true);
            validFork.IsFork = true;

            // Mock fork repository to be discovered via fork API
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { validFork });

            // Mock base repository info
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("electronicarts", "CnC_Generals_Zero_Hour", isBase: true));
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("TheSuperHackers", "GeneralsGameCode", isBase: true));

            // Mock other API calls to return empty results to focus on fork discovery
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRepository>());
            _mockApiClient.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRepository>());

            // Mock validation to pass for valid fork
            _mockApiClient.Setup(x => x.GetReleasesForRepositoryAsync(validFork.RepoOwner, validFork.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { CreateTestRelease("v1.0.0", hasAssets: true) });            // Act
            var result = await _service.DiscoverRepositoriesAsync();

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().ContainSingle("Valid fork with releases should be included");
            result.Data.First().RepoOwner.Should().Be("fork-owner");
            result.Data.First().IsFork.Should().BeTrue();
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithForkButNoReleases_RejectsRepository()
        {
            // Arrange
            var forkWithoutContent = CreateTestRepository("fork-owner", "fork-repo", isBase: false, isGeneralsRelated: true);
            forkWithoutContent.IsFork = true;

            // Mock fork repository to be discovered
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { forkWithoutContent });

            // Mock base repository info
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("electronicarts", "CnC_Generals_Zero_Hour", isBase: true));
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("TheSuperHackers", "GeneralsGameCode", isBase: true));

            // Mock other API calls
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRepository>());
            _mockApiClient.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRepository>());

            // Mock no releases and no successful workflows
            _mockApiClient.Setup(x => x.GetReleasesForRepositoryAsync(forkWithoutContent.RepoOwner, forkWithoutContent.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRelease>());
            _mockApiClient.Setup(x => x.GetWorkflowRunsForRepositoryAsync(forkWithoutContent.RepoOwner, forkWithoutContent.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubWorkflowRun>());

            // Act
            var result = await _service.DiscoverRepositoriesAsync();            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().BeEmpty("Fork without releases or workflows should be rejected");
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithMarkerRepositories_FindsBothMarkers()
        {
            // Arrange - Create both marker repositories as forks with content
            var marker1 = CreateTestRepository("jmarshall2323", "CnC_Generals_Zero_Hour", isBase: false, isGeneralsRelated: true);
            marker1.IsFork = true;
            
            var marker2 = CreateTestRepository("x64-dev", "GeneralsGameCode_GeneralsOnline", isBase: false, isGeneralsRelated: true);
            marker2.IsFork = true;

            // Mock API calls to return marker repositories
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { marker1 });
            _mockApiClient.Setup(x => x.GetRepositoryForksAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { marker2 });

            // Mock base repository info
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("electronicarts", "CnC_Generals_Zero_Hour", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("electronicarts", "CnC_Generals_Zero_Hour", isBase: true));
            _mockApiClient.Setup(x => x.GetRepositoryInfoAsync("TheSuperHackers", "GeneralsGameCode", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateTestRepository("TheSuperHackers", "GeneralsGameCode", isBase: true));

            // Mock search to return empty to focus on fork discovery
            _mockApiClient.Setup(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitHubRepository>());

            // Mock validation - marker repositories pass as they are valid
            _mockApiClient.Setup(x => x.GetReleasesForRepositoryAsync(marker1.RepoOwner, marker1.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { CreateTestRelease("v1.0.0", hasAssets: true) });
            _mockApiClient.Setup(x => x.GetReleasesForRepositoryAsync(marker2.RepoOwner, marker2.RepoName, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { CreateTestRelease("v1.0.0", hasAssets: true) });            // Act
            var result = await _service.DiscoverRepositoriesAsync();

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().HaveCount(2, "Both marker repositories should be found");
            result.Data.Should().Contain(r => r.RepoOwner == "jmarshall2323" && r.RepoName == "CnC_Generals_Zero_Hour");
            result.Data.Should().Contain(r => r.RepoOwner == "x64-dev" && r.RepoName == "GeneralsGameCode_GeneralsOnline");
        }

        #endregion
    }
}
