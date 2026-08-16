using FluentAssertions;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Features.Content.Services.GitHub;
using GenHub.Features.Content.Services.Publishers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Features.Content.Services.GitHub;

/// <summary>
/// Unit tests for <see cref="GitHubContentDeliverer"/>.
/// </summary>
public class GitHubContentDelivererTests
{
    private readonly Mock<IDownloadService> _downloadService = new();
    private readonly Mock<IContentManifestPool> _manifestPool = new();
    private readonly Mock<ILogger<GitHubContentDeliverer>> _logger = new();
    private readonly Mock<IFileHashProvider> _fileHashProvider = new();
    private readonly PublisherManifestFactoryResolver _factoryResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubContentDelivererTests"/> class.
    /// </summary>
    public GitHubContentDelivererTests()
    {
        _factoryResolver = new PublisherManifestFactoryResolver(
            [],
            new Mock<ILogger<PublisherManifestFactoryResolver>>().Object);
    }

    /// <summary>
    /// Tests that CanDeliver returns true for github.com URLs.
    /// </summary>
    [Fact]
    public void CanDeliver_ShouldReturnTrue_ForGitHubUrls()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files =
            [
                new ManifestFile { DownloadUrl = "https://github.com/user/repo/release.zip" },
            ],
        };

        deliverer.CanDeliver(manifest).Should().BeTrue();
    }

    /// <summary>
    /// Tests that CanDeliver returns false for githubusercontent.com URLs, since the deliverer
    /// only accepts the github.com domain family.
    /// </summary>
    [Fact]
    public void CanDeliver_ShouldReturnFalse_ForGitHubAssetsUrls()
    {
        // Note: objects.githubusercontent.com is NOT a *.github.com subdomain,
        // so the deliverer does not accept it. GitHub release assets use github.com URLs.
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files =
            [
                new ManifestFile { DownloadUrl = "https://objects.githubusercontent.com/some-asset.zip" },
            ],
        };

        deliverer.CanDeliver(manifest).Should().BeFalse();
    }

    /// <summary>
    /// Tests that CanDeliver returns false for non-GitHub URLs.
    /// </summary>
    [Fact]
    public void CanDeliver_ShouldReturnFalse_ForNonGitHubUrls()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files =
            [
                new ManifestFile { DownloadUrl = "https://example.com/release.zip" },
            ],
        };

        deliverer.CanDeliver(manifest).Should().BeFalse();
    }

    /// <summary>
    /// Tests that CanDeliver returns false when there are no files.
    /// </summary>
    [Fact]
    public void CanDeliver_ShouldReturnFalse_ForEmptyFileList()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest { Files = [] };

        deliverer.CanDeliver(manifest).Should().BeFalse();
    }

    /// <summary>
    /// Tests that CanDeliver returns false when file has no download URL.
    /// </summary>
    [Fact]
    public void CanDeliver_ShouldReturnFalse_ForFileWithNullDownloadUrl()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files = [new ManifestFile { DownloadUrl = null }],
        };

        deliverer.CanDeliver(manifest).Should().BeFalse();
    }

    /// <summary>
    /// Tests that CanDeliver returns true if at least one file has a GitHub URL.
    /// </summary>
    [Fact]
    public void CanDeliver_ShouldReturnTrue_IfAtLeastOneFileIsGitHub()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files =
            [
                new ManifestFile { DownloadUrl = "https://example.com/file1.zip" },
                new ManifestFile { DownloadUrl = "https://github.com/user/repo/file2.zip" },
            ],
        };

        deliverer.CanDeliver(manifest).Should().BeTrue();
    }

    /// <summary>
    /// Tests that ValidateContentAsync returns success with true for valid GitHub files.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ValidateContentAsync_ShouldReturnTrue_ForValidGitHubContent()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files =
            [
                new ManifestFile { DownloadUrl = "https://github.com/user/repo/file.zip", IsRequired = true },
            ],
        };

        var result = await deliverer.ValidateContentAsync(manifest);

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    /// <summary>
    /// Tests that ValidateContentAsync returns false when required files have non-GitHub URLs.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ValidateContentAsync_ShouldReturnFalse_WhenRequiredFilesHaveNonGitHubUrls()
    {
        var deliverer = CreateSut();
        var manifest = new ContentManifest
        {
            Files =
            [
                new ManifestFile { DownloadUrl = "https://other.com/file.zip", IsRequired = true },
            ],
        };

        var result = await deliverer.ValidateContentAsync(manifest);

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    /// <summary>
    /// Tests that SourceName returns the GitHub deliverer identifier.
    /// </summary>
    [Fact]
    public void SourceName_ShouldReturnGitHubDelivererName()
    {
        var deliverer = CreateSut();
        deliverer.SourceName.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that IsEnabled returns true.
    /// </summary>
    [Fact]
    public void IsEnabled_ShouldReturnTrue()
    {
        var deliverer = CreateSut();
        deliverer.IsEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Creates the system under test with the mocked dependencies.
    /// </summary>
    /// <returns>A configured <see cref="GitHubContentDeliverer"/> instance.</returns>
    private GitHubContentDeliverer CreateSut() =>
        new(_downloadService.Object, _factoryResolver, _fileHashProvider.Object, _logger.Object);
}
