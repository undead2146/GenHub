using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.Content.Services.GeneralsOnline;

/// <summary>
/// Specialized deliverer for Generals Online content.
/// Handles ZIP download, extraction, file hashing, and CAS integration.
/// </summary>
public class GeneralsOnlineDeliverer : IContentDeliverer
{
    private readonly IDownloadService _downloadService;
    private readonly ICasService _casService;
    private readonly IFileHashProvider _hashProvider;
    private readonly IContentManifestBuilder _manifestBuilder;
    private readonly ILogger<GeneralsOnlineDeliverer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralsOnlineDeliverer"/> class.
    /// </summary>
    /// <param name="downloadService">The download service for retrieving files.</param>
    /// <param name="casService">The CAS service for content-addressable storage.</param>
    /// <param name="hashProvider">The file hash provider for computing file hashes.</param>
    /// <param name="manifestBuilder">The manifest builder for creating manifests.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public GeneralsOnlineDeliverer(
        IDownloadService downloadService,
        ICasService casService,
        IFileHashProvider hashProvider,
        IContentManifestBuilder manifestBuilder,
        ILogger<GeneralsOnlineDeliverer> logger)
    {
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _casService = casService ?? throw new ArgumentNullException(nameof(casService));
        _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
        _manifestBuilder = manifestBuilder ?? throw new ArgumentNullException(nameof(manifestBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string SourceName => "Generals Online Deliverer";

    /// <inheritdoc />
    public string Description => "Delivers Generals Online content via ZIP extraction and CAS storage";

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public ContentSourceCapabilities Capabilities => ContentSourceCapabilities.SupportsPackageAcquisition;

    private static bool IsExecutableFile(string relativePath)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        return extension is ".exe" or ".bat" or ".sh" or ".cmd" or ".dll";
    }

    /// <inheritdoc />
    public bool CanDeliver(ContentManifest manifest)
    {
        // Can deliver if it's a Generals Online manifest with a portable ZIP URL
        return manifest.Publisher?.Name?.Contains("Generals Online", StringComparison.OrdinalIgnoreCase) == true &&
               manifest.Files.Any(f => f.DownloadUrl?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);
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
            _logger.LogInformation("Starting Generals Online content delivery for {Version}", packageManifest.Version);

            // Step 1: Download ZIP file
            var zipFile = packageManifest.Files.FirstOrDefault(f => f.DownloadUrl?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);
            if (zipFile == null)
            {
                return OperationResult<ContentManifest>.CreateFailure("No ZIP file found in manifest");
            }

            var zipPath = Path.Combine(targetDirectory, "GeneralsOnline.zip");

            progress?.Report(new ContentAcquisitionProgress
            {
                Phase = ContentAcquisitionPhase.Downloading,
                ProgressPercentage = 10,
                CurrentOperation = "Downloading Generals Online ZIP package",
                CurrentFile = zipFile.RelativePath,
            });

            _logger.LogDebug("Downloading ZIP from {Url} to {Path}", zipFile.DownloadUrl, zipPath);
            var downloadResult = await _downloadService.DownloadFileAsync(
                zipFile.DownloadUrl!,
                zipPath,
                expectedHash: null,
                progress: null,
                cancellationToken);

            if (!downloadResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(
                    $"Failed to download ZIP: {downloadResult.FirstError}");
            }

            // Step 2: Extract ZIP
            var extractPath = Path.Combine(targetDirectory, "extracted");
            Directory.CreateDirectory(extractPath);

            progress?.Report(new ContentAcquisitionProgress
            {
                Phase = ContentAcquisitionPhase.Extracting,
                ProgressPercentage = 40,
                CurrentOperation = "Extracting Generals Online files",
            });

            _logger.LogDebug("Extracting ZIP to {Path}", extractPath);
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            // Step 3: Hash all extracted files and store in CAS
            progress?.Report(new ContentAcquisitionProgress
            {
                Phase = ContentAcquisitionPhase.Copying,
                ProgressPercentage = 60,
                CurrentOperation = "Hashing files and storing in CAS",
            });

            var extractedFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
            var manifestFiles = new List<ManifestFile>();
            var totalFiles = extractedFiles.Length;
            var processedFiles = 0;

            _logger.LogInformation("Processing {Count} extracted files", totalFiles);

            foreach (var filePath in extractedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(extractPath, filePath);
                var fileInfo = new FileInfo(filePath);

                // Compute hash and store in CAS
                var hash = await _hashProvider.ComputeFileHashAsync(filePath, cancellationToken);
                var casResult = await _casService.StoreContentAsync(filePath, hash, cancellationToken);
                
                if (!casResult.Success)
                {
                    _logger.LogWarning("Failed to store file in CAS: {File}, {Error}", relativePath, casResult.FirstError);
                    continue;
                }

                manifestFiles.Add(new ManifestFile
                {
                    RelativePath = relativePath,
                    Size = fileInfo.Length,
                    Hash = hash,
                    SourceType = ContentSourceType.ContentAddressable,
                    IsExecutable = IsExecutableFile(relativePath),
                    IsRequired = true,
                });

                processedFiles++;
                var percentage = 60 + (int)((double)processedFiles / totalFiles * 30);
                progress?.Report(new ContentAcquisitionProgress
                {
                    Phase = ContentAcquisitionPhase.Copying,
                    ProgressPercentage = percentage,
                    CurrentOperation = $"Processing {relativePath}",
                    CurrentFile = relativePath,
                    FilesProcessed = processedFiles,
                    TotalFiles = totalFiles,
                });
            }

            // Step 4: Build updated manifest
            var updatedManifest = BuildManifestWithCasFiles(packageManifest, manifestFiles);

            // Cleanup temporary files
            try
            {
                File.Delete(zipPath);
                Directory.Delete(extractPath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary files");
            }

            progress?.Report(new ContentAcquisitionProgress
            {
                Phase = ContentAcquisitionPhase.Completed,
                ProgressPercentage = 100,
                CurrentOperation = "Generals Online content delivered successfully",
                FilesProcessed = processedFiles,
                TotalFiles = totalFiles,
            });

            _logger.LogInformation("Successfully delivered Generals Online content: {Count} files stored in CAS", manifestFiles.Count);
            return OperationResult<ContentManifest>.CreateSuccess(updatedManifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver Generals Online content");
            return OperationResult<ContentManifest>.CreateFailure($"Content delivery failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> ValidateContentAsync(
        ContentManifest manifest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var hasZipFile = manifest.Files.Any(f =>
                !string.IsNullOrEmpty(f.DownloadUrl) &&
                f.DownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(OperationResult<bool>.CreateSuccess(hasZipFile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for Generals Online manifest {ManifestId}", manifest.Id);
            return Task.FromResult(OperationResult<bool>.CreateFailure($"Validation failed: {ex.Message}"));
        }
    }

    private ContentManifest BuildManifestWithCasFiles(ContentManifest original, List<ManifestFile> casFiles)
    {
        return new ContentManifest
        {
            Id = original.Id,
            Name = original.Name,
            Version = original.Version,
            ContentType = original.ContentType,
            TargetGame = original.TargetGame,
            Publisher = original.Publisher,
            Metadata = original.Metadata,
            Files = casFiles,
            Dependencies = original.Dependencies,
            RequiredDirectories = original.RequiredDirectories,
            InstallationInstructions = original.InstallationInstructions,
        };
    }
}
