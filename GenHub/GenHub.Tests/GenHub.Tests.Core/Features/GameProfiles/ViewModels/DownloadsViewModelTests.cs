using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Features.Content.Services.ContentDiscoverers;
using GenHub.Features.Downloads.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace GenHub.Tests.Core.Features.Downloads.ViewModels;

/// <summary>
/// Tests for DownloadsViewModel.
/// </summary>
public class DownloadsViewModelTests
{
    /// <summary>
    /// Ensures InitializeAsync completes successfully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        // Arrange
        var serviceProviderMock = new Mock<IServiceProvider>();
        var loggerMock = new Mock<ILogger<DownloadsViewModel>>();
        var mockNotificationService = new Mock<INotificationService>();
        var mockGitHubApiClient = new Mock<IGitHubApiClient>();
        var mockLoggerGitHubDiscoverer = new Mock<ILogger<GitHubTopicsDiscoverer>>();
        var mockMemoryCache = new Mock<IMemoryCache>();

        var mockGitHubDiscoverer = new Mock<GitHubTopicsDiscoverer>(
            mockGitHubApiClient.Object,
            mockLoggerGitHubDiscoverer.Object,
            mockMemoryCache.Object);

        var vm = new DownloadsViewModel(serviceProviderMock.Object, loggerMock.Object, mockNotificationService.Object, mockGitHubDiscoverer.Object);

        // Act & Assert (Smoke test to ensure no exceptions are thrown)
        await vm.InitializeAsync();
    }
}
