using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.Results;
using GenHub.Core.Models.GitHub;

namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for installing GitHub artifacts
    /// </summary>
    public class GitHubArtifactInstaller : IGitHubArtifactInstaller
    {
        private readonly ILogger<GitHubArtifactInstaller> _logger;
        // Removed: private readonly IGitHubServiceFacade _gitHubService;
        private readonly IGitHubArtifactReader _artifactReader; // Added
        private readonly IGitHubApiClient _apiClient; // Added
        private readonly IGameVersionInstaller _gameVersionInstaller;
        private readonly IGameVersionServiceFacade _gameVersionService;

        public GitHubArtifactInstaller(
            ILogger<GitHubArtifactInstaller> logger,
            // Removed: IGitHubServiceFacade gitHubService,
            IGitHubArtifactReader artifactReader, // Added
            IGitHubApiClient apiClient, // Added
            IGameVersionInstaller gameVersionInstaller,
            IGameVersionServiceFacade gameVersionService)
        {
            _logger = logger;
            // Removed: _gitHubService = gitHubService;
            _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader)); // Added
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient)); // Added
            _gameVersionInstaller = gameVersionInstaller;
            _gameVersionService = gameVersionService;
        }

        /// <summary>
        /// Downloads and installs an artifact as a game version
        /// </summary>
        public async Task<OperationResult<GameVersion>> InstallArtifactAsync(
            GitHubArtifact artifact,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (artifact == null)
            {
                _logger.LogWarning("Cannot install null artifact");
                return OperationResult<GameVersion>.Failed("No artifact specified");
            }

            // It's crucial that if IGitHubArtifactReader.DownloadArtifactAsync requires RepositoryInfo,
            // then artifact.RepositoryInfo must not be null.
            if (artifact.RepositoryInfo == null)
            {
                _logger.LogError("Artifact {ArtifactName} ({ArtifactId}) is missing RepositoryInfo. Cannot download.", artifact.Name, artifact.Id);
                // Consider if there's a way to download without RepositoryInfo (e.g., using a default repository)
                // or if this should be a hard failure. For now, failing.
                return OperationResult<GameVersion>.Failed($"Artifact '{artifact.Name}' is missing essential repository information for download.");
            }

            try
            {
                _logger.LogInformation("Beginning installation of artifact: {ArtifactName} ({ArtifactId}) from repo {RepoOwner}/{RepoName}",
                    artifact.Name, artifact.Id, artifact.RepositoryInfo.RepoOwner, artifact.RepositoryInfo.RepoName);
                
                // This IProgress<double> is for the _artifactReader's download operation (expects 0.0-1.0)
                IProgress<double> downloadPercentageProgress = new Progress<double>(percentage =>
                {
                    // This lambda is called when downloadPercentageProgress.Report() is invoked by the downloader.
                    // 'percentage' is the value (0.0 to 1.0) reported by the downloader.
                    var installProgress = InstallProgress.DownloadProgress(
                        percentage * 100, // Convert 0-1 to 0-100 for display/outer progress
                        (long)(percentage * artifact.SizeInBytes),
                        artifact.SizeInBytes);
                        
                    progress?.Report(installProgress); // 'progress' is the IProgress<InstallProgress> for the caller of InstallArtifactAsync
                });
                
                string tempFolder = Path.Combine(Path.GetTempPath(), "GenHub", "Downloads");
                Directory.CreateDirectory(tempFolder);
                
                _logger.LogInformation("Downloading artifact to: {TempFolder}", tempFolder);
                
                // Ensure the method signature on IGitHubArtifactReader matches this call:
                // Task<string> DownloadArtifactAsync(GitHubRepoSettings repoSettings, long artifactId, string destinationFolder, IProgress<double>? progress, CancellationToken cancellationToken);
                string downloadedFile = await _artifactReader.DownloadArtifactAsync(
                    artifact.RepositoryInfo, // Must be non-null due to the check above
                    artifact.Id,
                    tempFolder,
                    downloadPercentageProgress, // Pass the IProgress<double> instance
                    cancellationToken);
                
                if (string.IsNullOrEmpty(downloadedFile) || !File.Exists(downloadedFile))
                {
                    throw new FileNotFoundException("Downloaded artifact file not found", downloadedFile);
                }
                
                // Create options with GitHub-specific settings
                var options = new ExtractOptions
                {
                    CustomInstallName = GenerateInstallName(artifact),
                    DeleteZipAfterExtraction = true
                };
                
                _logger.LogInformation("Installing with name: {InstallName}", options.CustomInstallName);
                
                // Install the artifact using the game version installer
                return await _gameVersionInstaller.InstallVersionAsync(
                    artifact,
                    downloadedFile,
                    options,
                    progress,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Installation cancelled: {ArtifactName}", artifact.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing artifact {ArtifactId}: {ErrorMessage}",
                    artifact.Id, ex.Message);
                return OperationResult<GameVersion>.Failed(
                    $"Failed to install artifact: {ex.Message}", ex);
            }
        }

     public async Task<OperationResult<GameVersion>> InstallReleaseAssetAsync(
    string assetDownloadUrl,
    string assetName,
    GitHubRepoSettings repoSettings, 
    IProgress<InstallProgress> progress, // This is the main progress object for the entire operation
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(assetDownloadUrl))
    {
        _logger.LogWarning("Asset download URL is null or empty");
        return OperationResult<GameVersion>.Failed("No asset download URL specified");
    }

    try
    {
        _logger.LogInformation("Beginning installation of release asset: {AssetName} ({AssetUrl})", assetName, assetDownloadUrl);

        // This IProgress<double> is for the manual stream copy operation (reports 0.0-1.0)
        IProgress<double> streamCopyProgress = new Progress<double>(percentage =>
        {
            // This lambda is called when streamCopyProgress.Report() is invoked.
            // 'percentage' is the value (0.0 to 1.0) reported by the stream copy loop.
            progress?.Report(new InstallProgress 
            { 
                Percentage = percentage * 100, 
                Message = $"Downloading {assetName} ({(percentage*100):0}%)",
                // CurrentBytes and TotalBytes can be derived if contentLength is known
                // CurrentBytes = (long)(percentage * (contentLength ?? 0)),
                // TotalBytes = contentLength ?? 0
            });
        });

        string tempFolder = Path.Combine(Path.GetTempPath(), "GenHub", "Downloads");
        Directory.CreateDirectory(tempFolder);

        _logger.LogInformation("Downloading release asset to: {TempFolder}", tempFolder);

        var downloadResult = await _apiClient.GetStreamAsync(repoSettings, assetDownloadUrl, cancellationToken); 
        using var stream = downloadResult.Stream; // Ensure stream is disposed
        var contentLength = downloadResult.ContentLength;

        if (stream == null || contentLength.GetValueOrDefault() <= 0) 
        {
            throw new FileNotFoundException($"Downloaded release asset stream is null or content length is invalid for {assetName}");
        }

        string tempFilePath = Path.Combine(tempFolder, Path.GetFileName(assetName)); 

        long totalBytesRead = 0;
        byte[] buffer = new byte[81920]; 
        int bytesRead;

        using (var fileStream = File.Create(tempFilePath))
        {
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                if (contentLength.HasValue && contentLength.Value > 0)
                {
                    streamCopyProgress.Report((double)totalBytesRead / contentLength.Value); // Report to IProgress<double>
                }
            }
        }
        // stream is disposed by 'using' statement

        if (totalBytesRead == 0)
        {
             File.Delete(tempFilePath); 
             throw new IOException($"Failed to download release asset {assetName}, 0 bytes received.");
        }

        // Create GitHubReleaseAsset instance from available data
        var releaseAsset = new GitHubReleaseAsset
        {
            Id = 0, // We don't have this info
            Name = assetName,
            BrowserDownloadUrl = assetDownloadUrl,
            Size = contentLength ?? 0,
            ContentType = "application/octet-stream", // Default
            CreatedAt = DateTime.Now // We don't have the exact creation time
        };

        // Create GitHubRelease instance from available data
        var release = new GitHubRelease
        {
            Id = 0, // We don't have this info
            Name = $"Release for {assetName}",
            TagName = "",
            PublishedAt = DateTime.Now,
            Body = ""
        };

        // Create copy of repository settings instead of redeclaring the variable
        var repoSettingsCopy = new GitHubRepoSettings
        {
            RepoOwner = repoSettings.RepoOwner,
            RepoName = repoSettings.RepoName,
            DisplayName = repoSettings.DisplayName
        };

        // Define extract options here since we're using them below
        var extractOptions = new ExtractOptions
        {
            CustomInstallName = assetName,
            DeleteZipAfterExtraction = true
        };

        // Now call the method with proper object types
        return await _gameVersionInstaller.InstallVersionFromReleaseAssetAsync(
            releaseAsset,
            release,
            tempFilePath,
            extractOptions,
            progress,
            cancellationToken);
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Installation cancelled: {AssetName}", assetName);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error installing release asset {AssetName}: {ErrorMessage}", assetName, ex.Message);
        return OperationResult<GameVersion>.Failed($"Failed to install release asset: {ex.Message}", ex);
    }
}

        /// <summary>
        /// Installs a game version from a downloaded GitHub release asset file.
        /// </summary>
        public async Task<OperationResult<GameVersion>> InstallVersionFromReleaseAssetAsync(
            GitHubRelease release,
            GitHubReleaseAsset asset,
            string downloadedFilePath,
            ExtractOptions? options,
            IProgress<InstallProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (release == null || asset == null)
            {
                _logger.LogWarning("Release or asset is null");
                return OperationResult<GameVersion>.Failed("Release or asset not specified");
            }

            if (string.IsNullOrEmpty(downloadedFilePath) || !File.Exists(downloadedFilePath))
            {
                _logger.LogWarning("Downloaded file path is invalid: {Path}", downloadedFilePath);
                return OperationResult<GameVersion>.Failed("Downloaded file not found");
            }

            try
            {
                _logger.LogInformation("Installing version from release asset: {AssetName} (Release: {ReleaseName})", asset.Name, release.Name);

                // Use asset name as install name if not specified
                var installOptions = options ?? new ExtractOptions
                {
                    CustomInstallName = asset.Name,
                    DeleteZipAfterExtraction = true
                };

                // Optionally, you could create a GitHubArtifact model to pass to the installer for metadata
                var pseudoArtifact = new GitHubArtifact
                {
                    Id = asset.Id,
                    Name = asset.Name,
                    IsRelease = true,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    SizeInBytes = asset.Size,
                    CreatedAt = asset.CreatedAt,
                    Expired = false,
                    RepositoryInfo = null // Could be set if needed
                };

                var result = await _gameVersionInstaller.InstallVersionFromReleaseAssetAsync(
                    asset,
                    release,
                    downloadedFilePath,
                    installOptions,
                    progress,
                    cancellationToken);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Installation cancelled for release asset: {AssetName}", asset.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing version from release asset {AssetName}: {ErrorMessage}", asset.Name, ex.Message);
                return OperationResult<GameVersion>.Failed($"Failed to install version from release asset: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if an artifact is already installed
        /// </summary>
        public async Task<bool> IsArtifactInstalledAsync(
            GitHubArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var installedVersions = await _gameVersionService.GetInstalledVersionsAsync(cancellationToken);
                
                foreach (var version in installedVersions)
                {
                    if (version.SourceType == GameInstallationType.GitHubArtifact &&
                        version.GitHubMetadata?.AssociatedArtifact?.Id == artifact.Id)
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if artifact {ArtifactId} is installed", artifact.Id);
                return false;
            }
        }

        /// <summary>
        /// Generates a suitable installation name for an artifact
        /// </summary>
        public string GenerateInstallName(GitHubArtifact artifact)
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string baseName = artifact.Name;

            // Add PR number if available
            if (artifact.PullRequestNumber.HasValue)
            {
                return $"{datePart}_PR{artifact.PullRequestNumber}_{baseName}";
            }
            // Add workflow number if no PR
            else if (artifact.WorkflowNumber > 0)
            {
                return $"{datePart}_WF{artifact.WorkflowNumber}_{baseName}";
            }
            // Just use date prefix as fallback
            else
            {
                return $"{datePart}_{baseName}";
            }
        }
    }
}
