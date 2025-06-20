using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GenHub.Core.Models;
using GenHub.Features.GitHub.Services;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Caching;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace GenHub.Tests.Core.Features.GitHub.Services
{    /// <summary>
    /// Comprehensive unit tests for GitHubApiClient
    /// Tests HTTP client interaction, API response parsing, and error handling
    /// </summary>
    public class GitHubApiClientTests
    {
        private readonly Mock<HttpClient> _mockHttpClient;
        private readonly Mock<ILogger<GitHubApiClient>> _mockLogger;
        private readonly Mock<ITokenStorageService> _mockTokenStorage;
        private readonly GitHubApiClient _apiClient;

        public GitHubApiClientTests()
        {
            _mockHttpClient = new Mock<HttpClient>();
            _mockLogger = new Mock<ILogger<GitHubApiClient>>();            _mockTokenStorage = new Mock<ITokenStorageService>();
            _apiClient = new GitHubApiClient(
                _mockHttpClient.Object,
                _mockLogger.Object,
                _mockTokenStorage.Object
            );
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert
            _apiClient.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GitHubApiClient(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GitHubApiClient(_httpClient, null!));
        }

        #endregion

        #region GetRepositoryAsync Tests

        [Fact]
        public async Task GetRepositoryAsync_WithValidOwnerAndRepo_ReturnsRepository()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var responseJson = """
                {
                    "id": 123456,
                    "name": "test-repo",
                    "full_name": "test-owner/test-repo",
                    "owner": {
                        "login": "test-owner"
                    },
                    "description": "Test repository",
                    "html_url": "https://github.com/test-owner/test-repo",
                    "fork": false,
                    "default_branch": "main"
                }
                """;

            SetupHttpResponse(HttpStatusCode.OK, responseJson);

            // Act
            var result = await _apiClient.GetRepositoryAsync(owner, repo);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("test-repo");
            result.Owner.Should().Be("test-owner");
            result.Description.Should().Be("Test repository");
            result.IsFork.Should().BeFalse();
        }

        [Fact]
        public async Task GetRepositoryAsync_WithNotFoundRepository_ReturnsNull()
        {
            // Arrange
            var owner = "nonexistent-owner";
            var repo = "nonexistent-repo";

            SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

            // Act
            var result = await _apiClient.GetRepositoryAsync(owner, repo);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRepositoryAsync_WithNetworkError_ThrowsHttpRequestException()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _apiClient.GetRepositoryAsync(owner, repo));
        }

        [Theory]
        [InlineData(null, "repo")]
        [InlineData("", "repo")]
        [InlineData("owner", null)]
        [InlineData("owner", "")]
        public async Task GetRepositoryAsync_WithInvalidParameters_ThrowsArgumentException(string owner, string repo)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _apiClient.GetRepositoryAsync(owner, repo));
        }

        #endregion

        #region GetWorkflowRunsAsync Tests

        [Fact]
        public async Task GetWorkflowRunsAsync_WithValidRepository_ReturnsWorkflowRuns()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var responseJson = """
                {
                    "total_count": 2,
                    "workflow_runs": [
                        {
                            "id": 1,
                            "name": "Build",
                            "head_branch": "main",
                            "head_sha": "abc123",
                            "status": "completed",
                            "conclusion": "success",
                            "created_at": "2023-01-01T00:00:00Z",
                            "updated_at": "2023-01-01T00:01:00Z"
                        },
                        {
                            "id": 2,
                            "name": "Test",
                            "head_branch": "feature",
                            "head_sha": "def456",
                            "status": "in_progress",
                            "conclusion": null,
                            "created_at": "2023-01-02T00:00:00Z",
                            "updated_at": "2023-01-02T00:01:00Z"
                        }
                    ]
                }
                """;

            SetupHttpResponse(HttpStatusCode.OK, responseJson);

            // Act
            var result = await _apiClient.GetWorkflowRunsAsync(owner, repo);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            
            var firstRun = result.First();
            firstRun.Id.Should().Be(1);
            firstRun.Name.Should().Be("Build");
            firstRun.HeadBranch.Should().Be("main");
            firstRun.Status.Should().Be("completed");
            firstRun.Conclusion.Should().Be("success");
        }

        [Fact]
        public async Task GetWorkflowRunsAsync_WithEmptyResponse_ReturnsEmptyList()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var responseJson = """
                {
                    "total_count": 0,
                    "workflow_runs": []
                }
                """;

            SetupHttpResponse(HttpStatusCode.OK, responseJson);

            // Act
            var result = await _apiClient.GetWorkflowRunsAsync(owner, repo);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region GetArtifactsAsync Tests

        [Fact]
        public async Task GetArtifactsAsync_WithValidWorkflowRun_ReturnsArtifacts()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var runId = 123L;
            var responseJson = """
                {
                    "total_count": 1,
                    "artifacts": [
                        {
                            "id": 456,
                            "name": "build-artifacts",
                            "size_in_bytes": 1024,
                            "archive_download_url": "https://api.github.com/repos/test-owner/test-repo/actions/artifacts/456/zip",
                            "created_at": "2023-01-01T00:00:00Z",
                            "expires_at": "2023-01-31T00:00:00Z",
                            "expired": false
                        }
                    ]
                }
                """;

            SetupHttpResponse(HttpStatusCode.OK, responseJson);

            // Act
            var result = await _apiClient.GetArtifactsAsync(owner, repo, runId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            
            var artifact = result.First();
            artifact.Id.Should().Be(456);
            artifact.Name.Should().Be("build-artifacts");
            artifact.SizeInBytes.Should().Be(1024);
            artifact.Expired.Should().BeFalse();
        }

        #endregion

        #region GetReleasesAsync Tests

        [Fact]
        public async Task GetReleasesAsync_WithValidRepository_ReturnsReleases()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var responseJson = """
                [
                    {
                        "id": 789,
                        "tag_name": "v1.0.0",
                        "name": "Release 1.0.0",
                        "body": "Initial release",
                        "prerelease": false,
                        "draft": false,
                        "published_at": "2023-01-01T00:00:00Z",
                        "assets": [
                            {
                                "id": 101,
                                "name": "release.zip",
                                "size": 2048,
                                "download_count": 100,
                                "browser_download_url": "https://github.com/test-owner/test-repo/releases/download/v1.0.0/release.zip"
                            }
                        ]
                    }
                ]
                """;

            SetupHttpResponse(HttpStatusCode.OK, responseJson);

            // Act
            var result = await _apiClient.GetReleasesAsync(owner, repo);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            
            var release = result.First();
            release.Id.Should().Be(789);
            release.TagName.Should().Be("v1.0.0");
            release.Name.Should().Be("Release 1.0.0");
            release.IsPrerelease.Should().BeFalse();
            release.Assets.Should().HaveCount(1);
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public async Task GetRepositoryAsync_WithRateLimitExceeded_ThrowsRateLimitException()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var responseJson = """
                {
                    "message": "API rate limit exceeded",
                    "documentation_url": "https://docs.github.com/rest/overview/resources-in-the-rest-api#rate-limiting"
                }
                """;

            SetupHttpResponse(HttpStatusCode.Forbidden, responseJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _apiClient.GetRepositoryAsync(owner, repo));
            exception.Message.Should().Contain("rate limit");
        }

        #endregion

        #region Authentication Tests

        [Fact]
        public async Task GetRepositoryAsync_WithAuthenticationHeader_IncludesTokenInRequest()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var token = "test-token";
            
            var authenticatedClient = new GitHubApiClient(_httpClient, _mockLogger.Object, token);
            
            SetupHttpResponse(HttpStatusCode.OK, """{"id": 1, "name": "test"}""");

            // Act
            await authenticatedClient.GetRepositoryAsync(owner, repo);

            // Assert
            _mockHttpHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Headers.Authorization != null &&
                    req.Headers.Authorization.Scheme == "Bearer" &&
                    req.Headers.Authorization.Parameter == token),
                ItExpr.IsAny<CancellationToken>());
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task GetRepositoryAsync_WithSameRequestTwice_MakesOnlyOneHttpCall()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var responseJson = """{"id": 1, "name": "test"}""";

            SetupHttpResponse(HttpStatusCode.OK, responseJson);

            // Act
            await _apiClient.GetRepositoryAsync(owner, repo);
            await _apiClient.GetRepositoryAsync(owner, repo);

            // Assert
            _mockHttpHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        #endregion

        #region Timeout Tests

        [Fact]
        public async Task GetRepositoryAsync_WithTimeout_ThrowsTaskCanceledException()
        {
            // Arrange
            var owner = "test-owner";
            var repo = "test-repo";
            var cancellationToken = new CancellationToken(true);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _apiClient.GetRepositoryAsync(owner, repo, cancellationToken));
        }

        #endregion

        #region Helper Methods

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };

            _mockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_WhenCalled_DisposesHttpClient()
        {
            // Act
            _apiClient.Dispose();

            // Assert
            // Verify that subsequent calls throw ObjectDisposedException
            Assert.ThrowsAsync<ObjectDisposedException>(() => _apiClient.GetRepositoryAsync("owner", "repo"));
        }

        #endregion
    }
}
