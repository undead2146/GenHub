using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.Github;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Content.Services.Helpers;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Services;

/// <summary>
/// Disposable scope for temporary directories.
/// </summary>
public sealed class TempDirectoryScope : IDisposable
{
    /// <summary>
    /// Gets the path to the temporary directory.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TempDirectoryScope"/> class.
    /// </summary>
    public TempDirectoryScope()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"genhub_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Disposes the temporary directory.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            /* Log but don't throw in Dispose */
        }
    }
}

/// <summary>
/// Service for processing GitHub content through the complete pipeline:
/// Download → Extract → CAS Storage → Manifest Generation.
/// </summary>
public class GitHubContentProcessor(
    IGitHubServiceFacade gitHubService,
    ICasService casService,
    IManifestGenerationService manifestService,
    ILogger<GitHubContentProcessor> logger) : IGitHubContentProcessor
{
    private readonly IGitHubServiceFacade _gitHubService = gitHubService;
    private readonly ICasService _casService = casService;
    private readonly IManifestGenerationService _manifestService = manifestService;
    private readonly ILogger<GitHubContentProcessor> _logger = logger;

    /// <summary>
    /// Processes a GitHub release asset through the complete pipeline.
    /// </summary>
    /// <param name="request">The content processing request.</param>
    /// <returns>The generated content manifest.</returns>
    public async Task<OperationResult<ContentManifest>> ProcessReleaseAssetAsync(
        ContentProcessingRequest request)
    {
        var owner = request.Owner;
        var repo = request.Repository;
        var asset = (GitHubReleaseAsset)request.Asset!;
        var contentName = request.ContentName;
        var manifestVersion = request.ManifestVersion;
        var contentType = request.ContentType;
        var targetGame = request.TargetGame;
        var cancellationToken = request.CancellationToken;

        try
        {
            _logger.LogInformation("Processing GitHub release asset {AssetName} from {Owner}/{Repo}", asset.Name, owner, repo);

            // Step 1: Download the asset
            var downloadResult = await DownloadAssetAsync(owner, repo, asset, cancellationToken);
            if (!downloadResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(downloadResult.Errors);
            }

            // Step 2: Extract if it's an archive
            var extractionResult = await ExtractArchiveIfNeededAsync(downloadResult.Data!, cancellationToken);
            if (!extractionResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(extractionResult.Errors);
            }

            // Step 3: Store content in CAS and generate manifest
            var manifestResult = await StoreAndManifestAsync(
                extractionResult.Data!,
                owner,
                repo,
                asset.Name,
                contentName,
                manifestVersion,
                contentType,
                targetGame,
                cancellationToken);

            if (!manifestResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(manifestResult.Errors);
            }

            _logger.LogInformation("Successfully processed GitHub release asset {AssetName}", asset.Name);
            return OperationResult<ContentManifest>.CreateSuccess(manifestResult.Data!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process GitHub release asset {AssetName}", asset.Name);
            return OperationResult<ContentManifest>.CreateFailure($"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a GitHub workflow artifact through the complete pipeline.
    /// </summary>
    /// <param name="request">The content processing request.</param>
    /// <returns>The generated content manifest.</returns>
    public async Task<OperationResult<ContentManifest>> ProcessWorkflowArtifactAsync(
        ContentProcessingRequest request)
    {
        var owner = request.Owner;
        var repo = request.Repository;
        var artifact = (GitHubArtifact)request.Asset!;
        var contentName = request.ContentName;
        var manifestVersion = request.ManifestVersion;
        var contentType = request.ContentType;
        var targetGame = request.TargetGame;
        var cancellationToken = request.CancellationToken;

        try
        {
            _logger.LogInformation("Processing GitHub workflow artifact {ArtifactName} from {Owner}/{Repo}", artifact.Name, owner, repo);

            // Step 1: Download the artifact
            var downloadResult = await DownloadArtifactAsync(owner, repo, artifact, cancellationToken);
            if (!downloadResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(downloadResult.Errors);
            }

            // Step 2: Extract if it's an archive
            var extractionResult = await ExtractArchiveIfNeededAsync(downloadResult.Data!, cancellationToken);
            if (!extractionResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(extractionResult.Errors);
            }

            // Step 3: Store content in CAS and generate manifest
            var manifestResult = await StoreAndManifestAsync(
                extractionResult.Data!,
                owner,
                repo,
                artifact.Name,
                contentName,
                manifestVersion,
                contentType,
                targetGame,
                cancellationToken);

            if (!manifestResult.Success)
            {
                return OperationResult<ContentManifest>.CreateFailure(manifestResult.Errors);
            }

            _logger.LogInformation("Successfully processed GitHub workflow artifact {ArtifactName}", artifact.Name);
            return OperationResult<ContentManifest>.CreateSuccess(manifestResult.Data!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process GitHub workflow artifact {ArtifactName}", artifact.Name);
            return OperationResult<ContentManifest>.CreateFailure($"Processing failed: {ex.Message}");
        }
    }

    private async Task<OperationResult<string>> DownloadAssetAsync(
        string owner,
        string repo,
        GitHubReleaseAsset asset,
        CancellationToken cancellationToken)
    {
        using var tempDir = new TempDirectoryScope();
        try
        {
            var tempPath = Path.Combine(tempDir.Path, asset.Name);

            var downloadResult = await _gitHubService.DownloadReleaseAssetAsync(owner, repo, asset, tempPath);
            if (!downloadResult.Success)
            {
                return OperationResult<string>.CreateFailure($"Download failed: {downloadResult.ErrorMessage}");
            }

            _logger.LogDebug("Downloaded release asset to {Path}", tempPath);
            return OperationResult<string>.CreateSuccess(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download release asset {AssetName}", asset.Name);
            return OperationResult<string>.CreateFailure($"Download failed: {ex.Message}");
        }
    }

    private async Task<OperationResult<string>> DownloadArtifactAsync(
        string owner,
        string repo,
        GitHubArtifact artifact,
        CancellationToken cancellationToken)
    {
        using var tempDir = new TempDirectoryScope();
        try
        {
            var tempPath = Path.Combine(tempDir.Path, artifact.Name + ".zip");

            var downloadResult = await _gitHubService.DownloadArtifactAsync(owner, repo, artifact, tempPath);
            if (!downloadResult.Success)
            {
                return OperationResult<string>.CreateFailure($"Download failed: {downloadResult.ErrorMessage}");
            }

            _logger.LogDebug("Downloaded workflow artifact to {Path}", tempPath);
            return OperationResult<string>.CreateSuccess(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download workflow artifact {ArtifactName}", artifact.Name);
            return OperationResult<string>.CreateFailure($"Download failed: {ex.Message}");
        }
    }

    private async Task<OperationResult<string>> ExtractArchiveIfNeededAsync(string archivePath, CancellationToken cancellationToken)
    {
        try
        {
            var extension = Path.GetExtension(archivePath).ToLowerInvariant();
            if (extension is not ".zip" and not ".tar.gz" and not ".tgz")
            {
                // Not an archive, return the path as-is
                _logger.LogDebug("File {Path} is not an archive, using as-is", archivePath);
                return OperationResult<string>.CreateSuccess(archivePath);
            }

            var extractPath = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath));
            Directory.CreateDirectory(extractPath);

            if (extension == ".zip")
            {
                ZipFile.ExtractToDirectory(archivePath, extractPath, overwriteFiles: true);
            }
            else
            {
                // For tar.gz, we'd need additional libraries, but for now assume zip
                _logger.LogWarning("Tar.gz extraction not implemented, treating as single file");
                return OperationResult<string>.CreateSuccess(archivePath);
            }

            // Clean up the archive file
            File.Delete(archivePath);

            _logger.LogDebug("Extracted archive to {Path}", extractPath);
            return OperationResult<string>.CreateSuccess(extractPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract archive {Path}", archivePath);
            return OperationResult<string>.CreateFailure($"Extraction failed: {ex.Message}");
        }
    }

    private async Task<OperationResult<ContentManifest>> StoreAndManifestAsync(
        string contentPath,
        string owner,
        string repo,
        string identifier,
        string contentName,
        int manifestVersion,
        ContentType contentType,
        GameType targetGame,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await _manifestService.CreateGitHubContentManifestAsync(
                contentPath,
                owner,
                repo,
                identifier,
                contentName,
                manifestVersion,
                contentType,
                targetGame);

            var files = Directory.GetFiles(contentPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                // Store in CAS
                var storeResult = await _casService.StoreContentAsync(file, cancellationToken: cancellationToken);
                if (!storeResult.Success)
                    return OperationResult<ContentManifest>.CreateFailure(storeResult.Errors);
                
                // Add to manifest with CAS reference
                var relativePath = Path.GetRelativePath(contentPath, file);
                var fileInfo = new FileInfo(file);
                var isExecutable = GitHubInferenceHelper.IsExecutableFile(file);
                
                await manifest.AddContentAddressableFileAsync(
                    relativePath,
                    storeResult.Data, // CAS hash
                    fileInfo.Length,
                    isExecutable);
                
                _logger.LogDebug("Stored file {Path} in CAS with hash {Hash}", file, storeResult.Data);
            }
            
            var builtManifest = manifest.Build();
            _logger.LogDebug("Generated manifest {ManifestId} for GitHub content", builtManifest.Id);

            return OperationResult<ContentManifest>.CreateSuccess(builtManifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store and manifest content from {Path}", contentPath);
            return OperationResult<ContentManifest>.CreateFailure($"CAS storage and manifest failed: {ex.Message}");
        }
    }

    private async Task<OperationResult<ContentManifest>> GenerateManifestAsync(
        string contentPath,
        string owner,
        string repo,
        string identifier,
        string contentName,
        int manifestVersion,
        ContentType contentType,
        GameType targetGame,
        CancellationToken cancellationToken)
    {
        try
        {
            var builder = await _manifestService.CreateGitHubContentManifestAsync(
                contentPath,
                owner,
                repo,
                identifier,
                contentName,
                manifestVersion,
                contentType,
                targetGame);

            var manifest = builder.Build();
            _logger.LogDebug("Generated manifest {ManifestId} for GitHub content", manifest.Id);

            return OperationResult<ContentManifest>.CreateSuccess(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate manifest for GitHub content {Owner}/{Repo}:{Identifier}", owner, repo, identifier);
            return OperationResult<ContentManifest>.CreateFailure($"Manifest generation failed: {ex.Message}");
        }
    }
}
