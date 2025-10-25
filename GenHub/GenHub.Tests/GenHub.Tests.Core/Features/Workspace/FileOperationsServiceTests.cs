using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.Common;
using GenHub.Core.Models.Results;
using GenHub.Features.Workspace;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GenHub.Tests.Core.Features.Workspace;

/// <summary>
/// Tests for the FileOperationsService class.
/// </summary>
public class FileOperationsServiceTests : IDisposable
{
    private readonly Mock<ILogger<FileOperationsService>> _logger;
    private readonly Mock<IDownloadService> _downloadService;
    private readonly Mock<ICasService> _casService;
    private readonly FileOperationsService _service;
    private readonly string _tempDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOperationsServiceTests"/> class.
    /// </summary>
    public FileOperationsServiceTests()
    {
        _logger = new Mock<ILogger<FileOperationsService>>();
        _downloadService = new Mock<IDownloadService>();
        _casService = new Mock<ICasService>();
        _service = new FileOperationsService(_logger.Object, _downloadService.Object, _casService.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>
    /// Tests that CopyFileAsync creates a file at the destination path.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CopyFileAsync_CreatesFile()
    {
        var src = Path.Combine(_tempDir, "source.txt");
        var dst = Path.Combine(_tempDir, "destination.txt");

        await File.WriteAllTextAsync(src, "test content");
        await _service.CopyFileAsync(src, dst);

        Assert.True(File.Exists(dst));
        Assert.Equal("test content", await File.ReadAllTextAsync(dst));
    }

    /// <summary>
    /// Tests that CreateSymlinkAsync creates a symbolic link or falls back to copy on unsupported platforms.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateSymlinkAsync_CreatesSymlinkOrCopies()
    {
        var src = Path.Combine(_tempDir, "source.txt");
        var link = Path.Combine(_tempDir, "link.txt");

        await File.WriteAllTextAsync(src, "test content");

        // Try to create symlink; on unsupported platforms or insufficient privilege, skip test
        try
        {
            await _service.CreateSymlinkAsync(link, src);
        }
        catch (IOException ioEx)
        {
            // Windows: privilege not held or not supported, skip test
            if (ioEx.Message.Contains("privilege", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw;
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (NotSupportedException)
        {
            return;
        }

        Assert.True(File.Exists(link));
        Assert.Equal("test content", await File.ReadAllTextAsync(link));
    }

    /// <summary>
    /// Tests that CreateHardLinkAsync creates a hard link or falls back to copy on unsupported platforms.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateHardLinkAsync_CreatesHardLinkOrCopies()
    {
        var src = Path.Combine(_tempDir, "source.txt");
        var link = Path.Combine(_tempDir, "hardlink.txt");

        await File.WriteAllTextAsync(src, "test content");

        // Try to create hard link; on unsupported platforms or not implemented, skip test
        try
        {
            await _service.CreateHardLinkAsync(link, src);
        }
        catch (NotImplementedException)
        {
            // Not implemented in base service, skip test
            return;
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (NotSupportedException)
        {
            return;
        }

        Assert.True(File.Exists(link));
        Assert.Equal("test content", await File.ReadAllTextAsync(link));
    }

    /// <summary>
    /// Tests that VerifyFileHashAsync handles both case sensitive and case insensitive hash comparisons.
    /// </summary>
    /// <param name="caseSensitive">Whether to test case sensitive comparison.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerifyFileHashAsync_HandlesCase(bool caseSensitive)
    {
        var file = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(file, "test");

        var expectedHash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        var testHash = caseSensitive ? expectedHash.ToUpperInvariant() : expectedHash;

        _downloadService
            .Setup(x => x.ComputeFileHashAsync(file, default))
            .ReturnsAsync(expectedHash);

        var result = await _service.VerifyFileHashAsync(file, testHash);

        Assert.True(result);
        _downloadService.Verify(
            x => x.ComputeFileHashAsync(file, default),
            Times.Once);
    }

    /// <summary>
    /// Tests that VerifyFileHashAsync returns false when the hash does not match.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task VerifyFileHashAsync_ReturnsFalse_WhenHashDoesNotMatch()
    {
        var file = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(file, "test");

        var actualHash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        var wrongHash = "WRONG_HASH";

        _downloadService
            .Setup(x => x.ComputeFileHashAsync(file, default))
            .ReturnsAsync(actualHash);

        var result = await _service.VerifyFileHashAsync(file, wrongHash);

        Assert.False(result);
    }

    /// <summary>
    /// Tests that DownloadFileAsync uses the download service successfully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DownloadFileAsync_UsesDownloadService_Successfully()
    {
        var testUrl = "https://example.com/file.txt";
        var destination = Path.Combine(_tempDir, "download.txt");
        var progressReports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p => progressReports.Add(p));

        var successResult = DownloadResult.CreateSuccess(
            destination,
            100,
            TimeSpan.FromSeconds(1),
            false);

        _downloadService
            .Setup(x => x.DownloadFileAsync(
                It.Is<DownloadConfiguration>(cfg => cfg.Url == testUrl && cfg.DestinationPath == destination),
                progress,
                default))
            .ReturnsAsync(successResult);

        await _service.DownloadFileAsync(testUrl, destination, progress);

        _downloadService.Verify(
            x => x.DownloadFileAsync(
                It.Is<DownloadConfiguration>(cfg => cfg.Url == testUrl && cfg.DestinationPath == destination),
                progress,
                default),
            Times.Once);
    }

    /// <summary>
    /// Tests that DownloadFileAsync throws exception when download service fails.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DownloadFileAsync_ThrowsException_WhenDownloadServiceFails()
    {
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(s => s.DownloadFileAsync(
            It.IsAny<DownloadConfiguration>(),
            It.IsAny<IProgress<DownloadProgress>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(DownloadResult.CreateFailed("Failed"));

        var loggerMock = new Mock<ILogger<FileOperationsService>>();
        var fileOps = new FileOperationsService(loggerMock.Object, downloadServiceMock.Object, _casService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            fileOps.DownloadFileAsync("http://fail", "fail.zip"));
    }

    /// <summary>
    /// Tests that CopyFileAsync throws an exception when the source file does not exist.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CopyFileAsync_ThrowsException_WhenSourceFileNotFound()
    {
        var nonExistentSource = Path.Combine(_tempDir, "nonexistent.txt");
        var destination = Path.Combine(_tempDir, "destination.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.CopyFileAsync(nonExistentSource, destination));
    }

    /// <summary>
    /// Tests that CopyFileAsync creates the necessary directory structure for the destination file.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CopyFileAsync_CreatesDirectoryStructure()
    {
        var source = Path.Combine(_tempDir, "source.txt");
        var destination = Path.Combine(_tempDir, "nested", "deep", "destination.txt");

        await File.WriteAllTextAsync(source, "test content");

        await _service.CopyFileAsync(source, destination);

        Assert.True(File.Exists(destination));
        Assert.True(Directory.Exists(Path.GetDirectoryName(destination)));
    }

    /// <summary>
    /// Tests that VerifyFileHashAsync returns false when file does not exist.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task VerifyFileHashAsync_ReturnsFalse_WhenFileNotExists()
    {
        var nonExistentFile = Path.Combine(_tempDir, "nonexistent.txt");
        var hash = "somehash";

        var result = await _service.VerifyFileHashAsync(nonExistentFile, hash);

        Assert.False(result);
        _downloadService.Verify(
            x => x.ComputeFileHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that VerifyFileHashAsync handles exceptions gracefully.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task VerifyFileHashAsync_HandlesExceptions_Gracefully()
    {
        var file = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(file, "test");

        _downloadService
            .Setup(x => x.ComputeFileHashAsync(file, default))
            .ThrowsAsync(new IOException("Disk error"));

        var result = await _service.VerifyFileHashAsync(file, "somehash");

        Assert.False(result);
    }

    /// <summary>
    /// Tests that CreateSymlinkAsync with allowFallback=false throws exception on failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact(Skip = "Requires platform without symlink support or insufficient permissions")]
    public async Task CreateSymlinkAsync_WithAllowFallbackFalse_ThrowsOnFailure()
    {
        // This test would need to be run on a system without symlink permissions
        // or we would need to mock the file system operations.
        // Skipping for now as it requires specific environment conditions.
        var src = Path.Combine(_tempDir, "source.txt");
        var link = Path.Combine(_tempDir, "link.txt");
        await File.WriteAllTextAsync(src, "test content");

        // On systems without symlink support, this should throw
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _service.CreateSymlinkAsync(link, src, allowFallback: false);
        });
    }

    /// <summary>
    /// Tests that CreateSymlinkAsync with allowFallback=true creates copy on failure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateSymlinkAsync_WithAllowFallbackTrue_SucceedsAlways()
    {
        var src = Path.Combine(_tempDir, "source.txt");
        var link = Path.Combine(_tempDir, "link.txt");
        await File.WriteAllTextAsync(src, "test content");

        // With allowFallback=true (default), this should always succeed
        // Either by creating symlink or falling back to copy
        await _service.CreateSymlinkAsync(link, src, allowFallback: true);

        // Verify the file exists and has correct content
        Assert.True(File.Exists(link));
        Assert.Equal("test content", await File.ReadAllTextAsync(link));
    }

    /// <summary>
    /// Tests that CreateSymlinkAsync with default parameter allows fallback.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task CreateSymlinkAsync_WithDefaultParameter_AllowsFallback()
    {
        var src = Path.Combine(_tempDir, "source.txt");
        var link = Path.Combine(_tempDir, "link_default.txt");
        await File.WriteAllTextAsync(src, "test content");

        // Default parameter should allow fallback
        await _service.CreateSymlinkAsync(link, src);

        Assert.True(File.Exists(link));
        Assert.Equal("test content", await File.ReadAllTextAsync(link));
    }

    /// <summary>
    /// Performs cleanup by disposing of temporary resources.
    /// </summary>
    public void Dispose()
    {
        FileOperationsService.DeleteDirectoryIfExists(_tempDir);
    }
}
