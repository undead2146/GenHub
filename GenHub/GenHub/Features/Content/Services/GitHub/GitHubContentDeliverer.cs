using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Models.Common;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Content.Services.Publishers;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GenHub.Features.Content.Services.GitHub;

/// <summary>
/// Delivers GitHub content with special handling for releases containing ZIP archives.
/// Uses publisher-specific manifest factories for extensible content handling.
/// </summary>
public class GitHubContentDeliverer(
    IDownloadService downloadService,
    PublisherManifestFactoryResolver factoryResolver,
    IFileHashProvider hashProvider,
    ILogger<GitHubContentDeliverer> logger) : IContentDeliverer
{
    /// <inheritdoc />
    public string SourceName => ContentSourceNames.GitHubDeliverer;

    /// <inheritdoc />
    public string Description => GitHubConstants.GitHubDelivererDescription;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public ContentSourceCapabilities Capabilities => ContentSourceCapabilities.SupportsPackageAcquisition;

    /// <inheritdoc />
    public bool CanDeliver(ContentManifest manifest)
    {
        // Can deliver if files have GitHub download URLs
        return manifest.Files.Any(f =>
            !string.IsNullOrEmpty(f.DownloadUrl) &&
            IsGitHubUrl(f.DownloadUrl));
    }

    /// <inheritdoc />
    public async Task<OperationResult<ContentManifest>> DeliverContentAsync(
        ContentManifest packageManifest,
        string targetDirectory,
        IProgress<ContentAcquisitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Download all files (validate no duplicate paths to prevent data loss)
            var filesToDownload = packageManifest.Files
                .Where(f => !string.IsNullOrEmpty(f.DownloadUrl))
                .ToList();

            // Check for duplicate relative paths
            var duplicatePaths = filesToDownload
                .GroupBy(f => f.RelativePath)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatePaths.Count > 0)
            {
                logger.LogError(
                    "Manifest {ManifestId} contains duplicate relative paths: {Duplicates}. This would cause data loss.",
                    packageManifest.Id,
                    string.Join(", ", duplicatePaths));
                return OperationResult<ContentManifest>.CreateFailure(
                    $"Manifest contains duplicate file paths that would cause data loss: {string.Join(", ", duplicatePaths)}");
            }

            var downloadedFiles = new List<string>();
            int currentFileIndex = 0;
            int totalFiles = filesToDownload.Count;

            foreach (var file in filesToDownload)
            {
                currentFileIndex++;
                var localPath = Path.Combine(targetDirectory, file.RelativePath);
                var localDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                // Create progress adapter for download progress
                IProgress<DownloadProgress>? downloadProgress = null;
                if (progress != null)
                {
                    downloadProgress = new Progress<DownloadProgress>(dp =>
                    {
                        // The orchestrator owns the overall five-stage scale. Report only
                        // relative delivery progress so it cannot regress the stage display.
                        double currentProgress = ((currentFileIndex - 1) + (dp.Percentage / 100.0)) / totalFiles * 100;

                        progress.Report(new ContentAcquisitionProgress
                        {
                            Phase = ContentAcquisitionPhase.Downloading,
                            ProgressPercentage = currentProgress,
                            CurrentOperation = $"{file.RelativePath} ({currentFileIndex}/{totalFiles}) - {dp.Percentage:F0}% ({dp.FormattedSpeed})",
                            FilesProcessed = currentFileIndex - 1,
                            TotalFiles = totalFiles,
                            TotalBytes = dp.TotalBytes,
                            BytesProcessed = dp.BytesReceived,
                            CurrentFile = file.RelativePath,
                        });
                    });
                }

                var downloadResult = await downloadService.DownloadFileAsync(
                    new Uri(file.DownloadUrl!), localPath, file.Hash, downloadProgress, cancellationToken);

                if (!downloadResult.Success)
                {
                    return OperationResult<ContentManifest>.CreateFailure(
                        $"Failed to download {file.RelativePath}: {downloadResult.FirstError}");
                }

                downloadedFiles.Add(localPath);
                logger.LogInformation("Downloaded {FileName} to {Path}", file.RelativePath, localPath);
            }

            // Check if this is content with archive files (ZIP, 7z, tar.gz, etc.)
            var archiveFiles = downloadedFiles
                .Where(IsArchiveFile)
                .ToList();

            if (archiveFiles.Count > 0)
            {
                logger.LogInformation(
                    "Content detected with {Count} archive file(s). Extracting...",
                    archiveFiles.Count);

                // Extract all archives using SharpCompress
                foreach (var archiveFile in archiveFiles)
                {
                    try
                    {
                        await ExtractArchiveAsync(
                            archiveFile,
                            targetDirectory,
                            progress,
                            cancellationToken);

                        logger.LogInformation("Extracted {ArchiveFile}", Path.GetFileName(archiveFile));
                        File.Delete(archiveFile);
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogWarning("Extraction of {ArchiveFile} was cancelled; cleaning up target directory", Path.GetFileName(archiveFile));
                        try
                        {
                            Directory.Delete(targetDirectory, recursive: true);
                        }
                        catch
                        {
                            // Best-effort cleanup
                        }

                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to extract {ArchiveFile}", Path.GetFileName(archiveFile));
                        try
                        {
                            Directory.Delete(targetDirectory, recursive: true);
                        }
                        catch
                        {
                            // Best-effort cleanup
                        }

                        return OperationResult<ContentManifest>.CreateFailure(
                            $"Failed to extract {Path.GetFileName(archiveFile)}: {ex.Message}");
                    }
                }

                logger.LogInformation(
                    "Successfully extracted {Count} archive file(s) for {ManifestId}. Deferring manifest generation to the orchestrator.",
                    archiveFiles.Count,
                    packageManifest.Id);

                return OperationResult<ContentManifest>.CreateSuccess(packageManifest);
            }

            // For content without archives, compute hashes directly for downloaded files
            foreach (var file in filesToDownload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var localPath = Path.Combine(targetDirectory, file.RelativePath);
                if (File.Exists(localPath))
                {
                    file.Hash = await hashProvider.ComputeFileHashAsync(localPath, cancellationToken);
                    file.Size = new FileInfo(localPath).Length;
                    file.SourceType = ContentSourceType.ContentAddressable;
                }
            }

            return OperationResult<ContentManifest>.CreateSuccess(packageManifest);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("GitHub content delivery was cancelled for manifest {ManifestId}", packageManifest.Id);
            try
            {
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }

            return OperationResult<ContentManifest>.CreateFailure("Content delivery was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deliver GitHub content for manifest {ManifestId}", packageManifest.Id);
            try
            {
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }

            return OperationResult<ContentManifest>.CreateFailure($"Content delivery failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> ValidateContentAsync(
        ContentManifest manifest, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate that all required URLs are GitHub URLs
            foreach (var file in manifest.Files.Where(f => f.IsRequired && !string.IsNullOrEmpty(f.DownloadUrl)))
            {
                if (file.DownloadUrl != null && !IsGitHubUrl(file.DownloadUrl))
                {
                    return Task.FromResult(OperationResult<bool>.CreateSuccess(false));
                }
            }

            return Task.FromResult(OperationResult<bool>.CreateSuccess(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Validation failed for GitHub content manifest {ManifestId}", manifest.Id);
            return Task.FromResult(OperationResult<bool>.CreateFailure($"Validation failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Validates that a URL is a legitimate GitHub URL.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is a GitHub URL, false otherwise.</returns>
    private static bool IsGitHubUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a file is a supported archive format.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file is a supported archive format, false otherwise.</returns>
    private static bool IsArchiveFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == FileTypes.ZipFileExtension ||
               ext == FileTypes.SevenZipFileExtension ||
               ext == FileTypes.TarFileExtension ||
               ext == FileTypes.GzipFileExtension ||
               ext == FileTypes.RarFileExtension;
    }

    private static bool IsPathWithinDirectory(string normalizedBase, string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(normalizedBase);
        var normalizedTarget = Path.GetFullPath(fullPath);
        var relative = Path.GetRelativePath(normalizedRoot, normalizedTarget);
        return !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    /// <summary>
    /// Extracts an archive file to a target directory with progress reporting and bounds enforcement.
    /// </summary>
    /// <param name="archiveFile">Path to the archive file.</param>
    /// <param name="targetDirectory">Directory to extract files to.</param>
    /// <param name="progress">Progress reporter for extraction updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ExtractArchiveAsync(
        string archiveFile,
        string targetDirectory,
        IProgress<ContentAcquisitionProgress>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(
            () =>
            {
                using var archive = ArchiveFactory.OpenArchive(archiveFile);
                int totalEntries = archive.Entries.Count(e => !e.IsDirectory);
                int currentEntry = 0;
                long totalUncompressedSize = 0;

                var rootPath = Path.GetFullPath(targetDirectory) + Path.DirectorySeparatorChar;

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    currentEntry++;
                    if (currentEntry > CatalogConstants.MaxZipEntryCount)
                    {
                        throw new InvalidDataException(
                            $"Archive exceeds maximum entry count of {CatalogConstants.MaxZipEntryCount}");
                    }

                    totalUncompressedSize += entry.Size;
                    if (totalUncompressedSize > CatalogConstants.MaxZipUncompressedSizeBytes)
                    {
                        throw new InvalidDataException(
                            $"Archive exceeds maximum uncompressed size of {CatalogConstants.MaxZipUncompressedSizeBytes} bytes");
                    }

                    var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.Key ?? string.Empty));

                    // Containment guard: reject entries whose canonical path escapes the target
                    // directory (zip-slip / absolute entry keys from a remote-controlled archive).
                    if (!destinationPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Archive entry has an unsafe path: {entry.Key}");
                    }

                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    entry.WriteToFile(
                        destinationPath,
                        new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true,
                        });

                    double currentPercentage = (double)currentEntry / totalEntries * 100;

                    progress?.Report(
                        new ContentAcquisitionProgress
                        {
                            Phase = ContentAcquisitionPhase.Extracting,
                            ProgressPercentage = currentPercentage,
                            CurrentOperation = $"{Path.GetFileName(entry.Key)} ({currentEntry}/{totalEntries})",
                            FilesProcessed = currentEntry,
                            TotalFiles = totalEntries,
                            CurrentFile = Path.GetFileName(entry.Key) ?? string.Empty,
                        });
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Handles extracted content by using publisher-specific factories to create manifests.
    /// May return multiple manifests if the publisher factory detects multi-variant content.
    /// </summary>
    private async Task<OperationResult<ContentManifest>> HandleExtractedContentAsync(
        ContentManifest originalManifest,
        string extractedDirectory,
        IProgress<ContentAcquisitionProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve the appropriate factory for this publisher/content type
            logger.LogInformation(
                "Resolving factory for manifest {ManifestId}, Publisher={PublisherType}, ContentType={ContentType}",
                originalManifest.Id,
                originalManifest.Publisher?.PublisherType,
                originalManifest.ContentType);

            var factory = factoryResolver.ResolveFactory(originalManifest);
            if (factory == null)
            {
                return OperationResult<ContentManifest>.CreateFailure(
                    $"No factory found for manifest {originalManifest.Id} (Publisher: {originalManifest.Publisher?.PublisherType ?? GameClientConstants.UnknownVersion})");
            }

            logger.LogInformation(
                "Using factory {FactoryType} for manifest {ManifestId}",
                factory.GetType().Name,
                originalManifest.Id);

            // Use the factory to create manifests from extracted content
            var manifests = await factory.CreateManifestsFromExtractedContentAsync(
                originalManifest,
                extractedDirectory,
                cancellationToken);

            if (manifests.Count == 0)
            {
                logger.LogWarning("Factory produced no manifests for {ManifestId}", originalManifest.Id);
                return OperationResult<ContentManifest>.CreateFailure("No manifests generated from extracted content");
            }

            logger.LogInformation(
                "Factory generated {Count} manifest(s) from extracted content: {ManifestIds}",
                manifests.Count,
                string.Join(", ", manifests.Select(m => m.Id.Value)));

            var primaryManifest = manifests[0];

            return OperationResult<ContentManifest>.CreateSuccess(primaryManifest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle extracted content using factory");
            return OperationResult<ContentManifest>.CreateFailure($"Factory content handling failed: {ex.Message}");
        }
    }
}
