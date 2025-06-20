using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Interfaces.Caching;
using GenHub.Core.Models;
using GenHub.Features.GitHub.Services.RepositoryServices;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.GitHub.Services
{
    /// <summary>
    /// Comprehensive unit tests for GitHubRepositoryManager
    /// Tests repository validation, caching, and discovery functionality
    /// </summary>
    public class GitHubRepositoryManagerTests
    {
        private readonly Mock<IGitHubApiClient> _mockApiClient;
        private readonly Mock<ICachingService> _mockCachingService;
        private readonly Mock<ILogger<GitHubRepositoryManager>> _mockLogger;
        private readonly GitHubRepositoryManager _repositoryManager;

        public GitHubRepositoryManagerTests()
        {
            _mockApiClient = new Mock<IGitHubApiClient>();
            _mockCachingService = new Mock<ICachingService>();
            _mockLogger = new Mock<ILogger<GitHubRepositoryManager>>();
            
            _repositoryManager = new GitHubRepositoryManager(
                _mockApiClient.Object,
                _mockCachingService.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert
            _repositoryManager.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullApiClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GitHubRepositoryManager(
                null!, _mockCachingService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCachingService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GitHubRepositoryManager(
                _mockApiClient.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GitHubRepositoryManager(
                _mockApiClient.Object, _mockCachingService.Object, null!));
        }

        #endregion

        #region GetRepositoryAsync Tests

        [Fact]
        public async Task GetRepositoryAsync_WithValidRepository_ReturnsRepositoryFromApi()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var expectedRepo = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner,
                Description = "Test repository",
                IsFork = false,
                DefaultBranch = "main"
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRepo);

            // Act
            var result = await _repositoryManager.GetRepositoryAsync(owner, name);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedRepo);
            _mockApiClient.Verify(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetRepositoryAsync_WithCachedRepository_ReturnsCachedResult()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var cacheKey = $"github_repo_{owner}_{name}";
            var cachedRepo = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner,
                Description = "Cached repository"
            };

            _mockCachingService.Setup(x => x.GetAsync<GitHubRepository>(cacheKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedRepo);

            // Act
            var result = await _repositoryManager.GetRepositoryAsync(owner, name);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(cachedRepo);
            _mockApiClient.Verify(x => x.GetRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetRepositoryAsync_WithApiFailure_ReturnsNull()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync((GitHubRepository?)null);

            // Act
            var result = await _repositoryManager.GetRepositoryAsync(owner, name);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(null, "repo")]
        [InlineData("", "repo")]
        [InlineData("owner", null)]
        [InlineData("owner", "")]
        public async Task GetRepositoryAsync_WithInvalidParameters_ThrowsArgumentException(string owner, string name)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _repositoryManager.GetRepositoryAsync(owner, name));
        }

        #endregion

        #region ValidateRepositoryAsync Tests

        [Fact]
        public async Task ValidateRepositoryAsync_WithValidFork_ReturnsTrue()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var repository = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner,
                IsFork = true,
                ParentRepository = new GitHubRepository { Owner = "original-owner", Name = "original-repo" }
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repository);

            // Act
            var result = await _repositoryManager.ValidateRepositoryAsync(owner, name);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateRepositoryAsync_WithNonForkRepository_ReturnsFalse()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var repository = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner,
                IsFork = false
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repository);

            // Act
            var result = await _repositoryManager.ValidateRepositoryAsync(owner, name);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateRepositoryAsync_WithNonExistentRepository_ReturnsFalse()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync((GitHubRepository?)null);

            // Act
            var result = await _repositoryManager.ValidateRepositoryAsync(owner, name);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region DiscoverRepositoriesAsync Tests

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithValidSearchTerm_ReturnsRepositories()
        {
            // Arrange
            var searchTerm = "command-conquer";
            var repositories = new List<GitHubRepository>
            {
                new() { Id = 1, Name = "repo1", Owner = "owner1", IsFork = true },
                new() { Id = 2, Name = "repo2", Owner = "owner2", IsFork = true },
                new() { Id = 3, Name = "repo3", Owner = "owner3", IsFork = false }
            };

            _mockApiClient.Setup(x => x.SearchRepositoriesAsync(searchTerm, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repositories);

            // Act
            var result = await _repositoryManager.DiscoverRepositoriesAsync(searchTerm);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2); // Only forks should be returned
            result.All(r => r.IsFork).Should().BeTrue();
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithEmptySearchTerm_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _repositoryManager.DiscoverRepositoriesAsync(""));
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithNullSearchTerm_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _repositoryManager.DiscoverRepositoriesAsync(null!));
        }

        #endregion

        #region GetRepositoryInfoAsync Tests

        [Fact]
        public async Task GetRepositoryInfoAsync_WithValidUrl_ParsesAndReturnsRepository()
        {
            // Arrange
            var url = "https://github.com/test-owner/test-repo";
            var expectedRepo = new GitHubRepository
            {
                Id = 123,
                Name = "test-repo",
                Owner = "test-owner",
                IsFork = true
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync("test-owner", "test-repo", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRepo);

            // Act
            var result = await _repositoryManager.GetRepositoryInfoAsync(url);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedRepo);
        }

        [Theory]
        [InlineData("https://github.com/owner")]
        [InlineData("https://notgithub.com/owner/repo")]
        [InlineData("invalid-url")]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetRepositoryInfoAsync_WithInvalidUrl_ReturnsNull(string url)
        {
            // Act
            var result = await _repositoryManager.GetRepositoryInfoAsync(url);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetForkInfoAsync Tests

        [Fact]
        public async Task GetForkInfoAsync_WithValidFork_ReturnsForkInformation()
        {
            // Arrange
            var owner = "fork-owner";
            var name = "fork-repo";
            var forkRepository = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner,
                IsFork = true,
                ParentRepository = new GitHubRepository
                {
                    Id = 456,
                    Name = "original-repo",
                    Owner = "original-owner",
                    IsFork = false
                }
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(forkRepository);

            // Act
            var result = await _repositoryManager.GetForkInfoAsync(owner, name);

            // Assert
            result.Should().NotBeNull();
            result.IsFork.Should().BeTrue();
            result.ParentRepository.Should().NotBeNull();
            result.ParentRepository!.Owner.Should().Be("original-owner");
        }

        [Fact]
        public async Task GetForkInfoAsync_WithNonFork_ReturnsNull()
        {
            // Arrange
            var owner = "owner";
            var name = "repo";
            var repository = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner,
                IsFork = false
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repository);

            // Act
            var result = await _repositoryManager.GetForkInfoAsync(owner, name);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task GetRepositoryAsync_WhenSuccessful_CachesResult()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var cacheKey = $"github_repo_{owner}_{name}";
            var repository = new GitHubRepository
            {
                Id = 123,
                Name = name,
                Owner = owner
            };

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repository);

            // Act
            await _repositoryManager.GetRepositoryAsync(owner, name);

            // Assert
            _mockCachingService.Verify(x => x.SetAsync(
                cacheKey, 
                repository, 
                It.IsAny<TimeSpan>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DiscoverRepositoriesAsync_WithCachedResults_ReturnsCachedRepositories()
        {
            // Arrange
            var searchTerm = "command-conquer";
            var cacheKey = $"github_search_{searchTerm}";
            var cachedRepositories = new List<GitHubRepository>
            {
                new() { Id = 1, Name = "cached-repo", Owner = "cached-owner", IsFork = true }
            };

            _mockCachingService.Setup(x => x.GetAsync<List<GitHubRepository>>(cacheKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(cachedRepositories);

            // Act
            var result = await _repositoryManager.DiscoverRepositoriesAsync(searchTerm);

            // Assert
            result.Should().BeEquivalentTo(cachedRepositories);
            _mockApiClient.Verify(x => x.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task GetRepositoryAsync_WhenApiThrows_LogsErrorAndRethrows()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var exception = new HttpRequestException("API error");

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _repositoryManager.GetRepositoryAsync(owner, name));
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting repository")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ValidateRepositoryAsync_WhenApiThrows_ReturnsFalse()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";

            _mockApiClient.Setup(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("API error"));

            // Act
            var result = await _repositoryManager.ValidateRepositoryAsync(owner, name);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Repository Filtering Tests

        [Fact]
        public async Task DiscoverRepositoriesAsync_FiltersOutNonForks_ReturnsOnlyForks()
        {
            // Arrange
            var searchTerm = "command-conquer";
            var repositories = new List<GitHubRepository>
            {
                new() { Id = 1, Name = "fork1", Owner = "owner1", IsFork = true },
                new() { Id = 2, Name = "original", Owner = "owner2", IsFork = false },
                new() { Id = 3, Name = "fork2", Owner = "owner3", IsFork = true },
                new() { Id = 4, Name = "another-original", Owner = "owner4", IsFork = false }
            };

            _mockApiClient.Setup(x => x.SearchRepositoriesAsync(searchTerm, It.IsAny<CancellationToken>()))
                .ReturnsAsync(repositories);

            // Act
            var result = await _repositoryManager.DiscoverRepositoriesAsync(searchTerm);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.IsFork);
            result.Select(r => r.Name).Should().Contain(new[] { "fork1", "fork2" });
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public async Task GetRepositoryAsync_WithRateLimitExceeded_RetriesAfterDelay()
        {
            // Arrange
            var owner = "test-owner";
            var name = "test-repo";
            var repository = new GitHubRepository { Id = 123, Name = name, Owner = owner };

            _mockApiClient.SetupSequence(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("API rate limit exceeded"))
                .ReturnsAsync(repository);

            // Act
            var result = await _repositoryManager.GetRepositoryAsync(owner, name);

            // Assert
            result.Should().BeEquivalentTo(repository);
            _mockApiClient.Verify(x => x.GetRepositoryAsync(owner, name, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        #endregion

        #region Cleanup Tests

        [Fact]
        public async Task ClearCacheAsync_RemovesAllCachedRepositoryData()
        {
            // Act
            await _repositoryManager.ClearCacheAsync();

            // Assert
            _mockCachingService.Verify(x => x.RemoveByPatternAsync("github_repo_*", It.IsAny<CancellationToken>()), Times.Once);
            _mockCachingService.Verify(x => x.RemoveByPatternAsync("github_search_*", It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
