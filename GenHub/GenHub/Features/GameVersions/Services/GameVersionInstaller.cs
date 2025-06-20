using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GameVersions;
using GenHub.Core.Interfaces.Repositories;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.GameProfiles;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.Helpers;

namespace GenHub.Features.GameVersions.Services
{
    /// <summary>
    /// Service for installing game versions
    /// </summary>
    public class GameVersionInstaller : IGameVersionInstaller
    {
        private readonly ILogger<GameVersionInstaller> _logger;
        private readonly IGameVersionRepository _versionRepository;
        private readonly IGameExecutableLocator _executableLocator;

        public GameVersionInstaller(
            ILogger<GameVersionInstaller> logger,
            IGameVersionRepository versionRepository,
            IGameExecutableLocator executableLocator)
        {
            _logger = logger;
            _versionRepository = versionRepository;
            _executableLocator = executableLocator;
        }

        /// <summary>
        /// Installs a game version from a GitHub artifact
        /// </summary>
        public async Task<OperationResult<GameVersion>> InstallVersionAsync(
            GitHubArtifact artifact,
            string zipPath,
            ExtractOptions? options = null,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Installing version from GitHub artifact: {ArtifactName} ({ArtifactId})",
                artifact.Name, artifact.Id);

            try
            {
                // Create install options if none were provided
                options ??= new ExtractOptions();

                // Generate installation name if not provided
                string installName = options.CustomInstallName ?? GenerateInstallName(artifact);

                // Determine the proper installation path based on repository information
                string versionsPath = _versionRepository.GetVersionsStoragePath();

                // Determine install path based on whether this is a release or workflow artifact
                string installBasePath;
                if (artifact.IsRelease)
                {
                    // For releases: GenHub\Versions\GitHub\{RepoOwner}\{RepoName}\Releases
                    string repoOwner = artifact.RepositoryInfo?.RepoOwner ?? "Unknown";
                    string repoName = artifact.RepositoryInfo?.RepoName ?? "Unknown";
                    installBasePath = Path.Combine(versionsPath, "GitHub", repoOwner, repoName, "Releases");
                }
                else
                {
                    // For workflow artifacts: GenHub\Versions\GitHub\{RepoOwner}\{RepoName}
                    string repoOwner = artifact.RepositoryInfo?.RepoOwner ?? "Unknown";
                    string repoName = artifact.RepositoryInfo?.RepoName ?? "Unknown";
                    installBasePath = Path.Combine(versionsPath, "GitHub", repoOwner, repoName);
                }

                // Create the installation directory
                string installPath = Path.Combine(installBasePath, installName);
                Directory.CreateDirectory(installPath);

                _logger.LogInformation("Installing to path: {InstallPath}", installPath);

                // Report initial progress
                ReportProgress(progress, InstallProgress.ExtractionProgress(0, 0, 100));

                // Start timer for installation duration tracking
                var timer = System.Diagnostics.Stopwatch.StartNew();

                // Extract files with progress reporting
                await ExtractFilesAsync(zipPath, installPath, progress, cancellationToken);

                // Find the executable in the extracted files
                var executablePath = await _executableLocator.FindBestGameExecutableAsync(
                    installPath,
                    options.PreferZeroHour,
                    cancellationToken);

                if (string.IsNullOrEmpty(executablePath))
                {
                    throw new FileNotFoundException("Could not find a valid game executable in the extracted files.");
                }

                // Create the version object with GitHub metadata
                var version = CreateGameVersionFromArtifact(artifact, installPath, executablePath, installPath);

                // Save the version metadata alongside the installation
                await SaveVersionMetadataAsync(version, installPath);

                // Create additional metadata for the version
                CreateMetadataForVersion(version, installPath);

                // Save the version to the repository
                await _versionRepository.AddAsync(version, cancellationToken);

                // Report completion
                timer.Stop();
                ReportProgress(progress, InstallProgress.Completed(timer.Elapsed));

                _logger.LogInformation("Installation completed successfully: {VersionName}", version.Name);

                return OperationResult<GameVersion>.Succeeded(version);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Installation was cancelled for artifact: {ArtifactName}", artifact.Name);
                ReportProgress(progress, InstallProgress.Error("Installation cancelled"));
                return OperationResult<GameVersion>.Failed("Installation was cancelled by the user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during installation: {ErrorMessage}", ex.Message);
                ReportProgress(progress, InstallProgress.Error(ex.Message));
                return OperationResult<GameVersion>.Failed(ex.Message, ex);
            }
        }

        /// <summary>
        /// Saves version metadata as a JSON file in the installation directory
        /// </summary>
        private async Task SaveVersionMetadataAsync(GameVersion version, string installPath)
        {
            try
            {
                // Create filename based on the installation directory name
                string directoryName = Path.GetFileName(installPath);
                string metadataPath = Path.Combine(installPath, $"{directoryName}.json");

                // Use a variable for the associated artifact if present
                var artifact = version.GitHubMetadata?.AssociatedArtifact;

                // Prepare metadata object using the actual GameVersion model, including nested models
                var metadata = new
                {
                    // GameVersion fields
                    version.Id,
                    version.Name,
                    version.Description,
                    version.InstallPath,
                    version.GamePath,
                    version.ExecutablePath,
                    version.InstallDate,
                    version.SourceType,
                    version.GameType,
                    version.InstallSizeBytes,
                    version.IsValid,
                    version.IsZeroHour,
                    version.BuildDate,
                    // SourceSpecificMetadata (if present, serialize as its runtime type)
                    SourceSpecificMetadata = version.SourceSpecificMetadata,
                    // GitHub convenience accessors
                    GitHubMetadata = version.GitHubMetadata,
                    IsFromGitHub = version.IsFromGitHub,
                    FormattedSize = version.FormattedSize,
                    SourceTypeName = version.SourceTypeName,
                    // ExtractOptions (not typically serialized, but included if present)
                    Options = version.Options,

                    // If this version is from GitHub, include artifact and build info
                    Artifact = artifact != null
                        ? new
                        {
                            // GitHubArtifact fields
                            artifact.Id,
                            artifact.Name,
                            artifact.WorkflowId,
                            artifact.RunId,
                            artifact.WorkflowNumber,
                            artifact.SizeInBytes,
                            artifact.ArchiveDownloadUrl,
                            artifact.Expired,
                            artifact.CreatedAt,
                            artifact.ExpiresAt,
                            artifact.PullRequestNumber,
                            artifact.PullRequestTitle,
                            artifact.CommitSha,
                            artifact.CommitMessage,
                            artifact.EventType,
                            artifact.BuildPreset,
                            // RepositoryInfo
                            RepositoryInfo = artifact.RepositoryInfo != null
                                ? new
                                {
                                    artifact.RepositoryInfo.RepoOwner,
                                    artifact.RepositoryInfo.RepoName,
                                    artifact.RepositoryInfo.DisplayName
                                }
                                : null,
                            // BuildInfo
                            BuildInfo = artifact.BuildInfo != null
                                ? new
                                {
                                    artifact.BuildInfo.GameVariant,
                                    artifact.BuildInfo.Compiler,
                                    artifact.BuildInfo.Configuration,
                                    artifact.BuildInfo.Version,
                                    artifact.BuildInfo.HasTFlag,
                                    artifact.BuildInfo.HasEFlag
                                }
                                : null
                        }
                        : null
                };
                // Serialize with pretty printing for readability
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(metadata, options);

                // Write the metadata file
                await File.WriteAllTextAsync(metadataPath, json);

                _logger.LogInformation("Saved version metadata to {MetadataPath}", metadataPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving version metadata: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// Installs a game version from a local ZIP file
        /// </summary>
        public async Task<OperationResult<GameVersion>> InstallVersionFromZipAsync(
            string zipPath,
            ExtractOptions? options = null,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Installing version from local ZIP file: {ZipPath}", zipPath);

            try
            {
                // If this is actually a GitHub artifact installation being processed through the ZIP path,
                // we need to check for metadata and handle it properly
                GitHubArtifact? artifact = await ExtractArtifactMetadataFromZipAsync(zipPath);
                if (artifact != null)
                {
                    _logger.LogInformation("Detected GitHub artifact metadata in ZIP file");
                    return await InstallVersionAsync(artifact, zipPath, options, progress, cancellationToken);
                }

                // Otherwise, treat as a regular ZIP file installation
                options ??= new ExtractOptions();
                string installName = options.CustomInstallName ?? Path.GetFileNameWithoutExtension(zipPath);

                // Use the Local directory for non-GitHub installations
                string versionsPath = _versionRepository.GetVersionsStoragePath();
                string installPath = Path.Combine(versionsPath, "Local", installName);
                Directory.CreateDirectory(installPath);

                _logger.LogInformation("Installing to path: {InstallPath}", installPath);

                // Start timer for duration tracking
                var timer = System.Diagnostics.Stopwatch.StartNew();

                // Report initial progress
                ReportProgress(progress, InstallProgress.ExtractionProgress(0, 0, 100));

                // Extract files with progress reporting
                await ExtractFilesAsync(zipPath, installPath, progress, cancellationToken);

                // Find the executable
                var executablePath = await _executableLocator.FindBestGameExecutableAsync(
                    installPath,
                    options.PreferZeroHour,
                    cancellationToken);

                if (string.IsNullOrEmpty(executablePath))
                {
                    throw new FileNotFoundException("Could not find a valid game executable in the extracted files.");
                }

                // Create the version object
                var version = new GameVersion
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = CleanupVersionName(installName),
                    InstallPath = installPath,
                    ExecutablePath = executablePath,
                    InstallDate = DateTime.Now,
                    InstallSizeBytes = GetDirectorySize(installPath),
                    SourceType = GameInstallationType.LocalZipFile,
                    Options = options,
                    IsValid = true
                };

                // Save version metadata
                await SaveVersionMetadataAsync(version, installPath);

                // Create additional metadata for the version
                CreateMetadataForVersion(version, installPath);

                // Save to repository
                await _versionRepository.AddAsync(version, cancellationToken);

                // Report completion
                timer.Stop();
                ReportProgress(progress, InstallProgress.Completed(timer.Elapsed));

                _logger.LogInformation("Installation completed successfully: {VersionName}", version.Name);

                return OperationResult<GameVersion>.Succeeded(version);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Installation was cancelled for ZIP: {ZipPath}", zipPath);
                ReportProgress(progress, InstallProgress.Error("Installation cancelled"));
                return OperationResult<GameVersion>.Failed("Installation was cancelled by the user");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during installation from ZIP: {ErrorMessage}", ex.Message);
                ReportProgress(progress, InstallProgress.Error(ex.Message));
                return OperationResult<GameVersion>.Failed(ex.Message, ex);
            }
            finally
            {
                // Delete the ZIP file if requested
                if (options?.DeleteZipAfterExtraction == true && File.Exists(zipPath))
                {
                    try
                    {
                        File.Delete(zipPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete ZIP file after extraction: {Path}", zipPath);
                    }
                }
            }
        }

        /// <summary>
        /// Installs a game version from an archive with the specified options
        /// </summary>
        public async Task<OperationResult<GameVersion>> InstallGameVersionFromArchiveAsync(
            ExtractOptions options,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                return OperationResult<GameVersion>.Failed("Extract options cannot be null");
            }

            if (string.IsNullOrEmpty(options.ArchivePath))
            {
                return OperationResult<GameVersion>.Failed("Archive path is not specified in extract options");
            }

            // Simply delegate to the ZIP installation method
            return await InstallVersionFromZipAsync(
                options.ArchivePath,
                options,
                progress,
                cancellationToken);
        }

        /// <summary>
        /// Attempts to extract GitHub artifact metadata from a ZIP file
        /// </summary>
        private async Task<GitHubArtifact?> ExtractArtifactMetadataFromZipAsync(string zipPath)
        {
            try
            {
                // Check if the ZIP contains a GitHub artifact metadata file
                using var archive = ZipFile.OpenRead(zipPath);
                var metadataEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals("github-artifact.json", StringComparison.OrdinalIgnoreCase));

                if (metadataEntry != null)
                {
                    using var reader = new StreamReader(metadataEntry.Open());
                    string json = await reader.ReadToEndAsync();
                    return JsonSerializer.Deserialize<GitHubArtifact>(json);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting artifact metadata from ZIP: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Extracts files from a ZIP archive with progress reporting
        /// </summary>
        private async Task ExtractFilesAsync(
            string zipPath,
            string targetPath,
            IProgress<InstallProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            // Count total files and report initial progress
            int totalFiles = archive.Entries.Count;
            int extractedFiles = 0;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip directories
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                string destination = Path.Combine(targetPath, entry.FullName);

                // Create directory if needed
                string? destinationDirectory = Path.GetDirectoryName(destination);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                // Extract the file
                entry.ExtractToFile(destination, true);

                // Update progress
                extractedFiles++;
                double percentage = (double)extractedFiles / totalFiles * 100;
                ReportProgress(progress, InstallProgress.ExtractionProgress(percentage, extractedFiles, totalFiles));

                // Small delay to prevent UI freezing
                await Task.Delay(1, cancellationToken);
            }
        }

        /// <summary>
        /// Reports progress if a progress handler is provided
        /// </summary>
        private void ReportProgress(IProgress<InstallProgress>? progress, InstallProgress status)
        {
            progress?.Report(status);
        }

        /// <summary>
        /// Gets the total size of a directory and all its contents
        /// </summary>
        private long GetDirectorySize(string path)
        {
            var directory = new DirectoryInfo(path);
            return directory.Exists
                ? directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length)
                : 0;
        }

        /// <summary>
        /// Generates a directory name for installation based on artifact properties
        /// </summary>
        private string GenerateInstallName(GitHubArtifact artifact)
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string name = CleanupVersionName(artifact.Name);

            // For pull request artifacts, include PR number
            if (artifact.PullRequestNumber.HasValue)
            {
                name = $"{datePart}_PR{artifact.PullRequestNumber}_{name}";
            }
            // For standard builds, use date and name
            else
            {
                name = $"{datePart}_{name}";
            }

            return name;
        }

        /// <summary>
        /// Installs a game version from a GitHub Release asset.
        /// </summary>
        public async Task<OperationResult<GameVersion>> InstallVersionFromReleaseAssetAsync(
            GitHubReleaseAsset asset,
            GitHubRelease release,
            string downloadedFilePath,
            ExtractOptions options,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
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

                // Use release and asset info to create an install name if not specified
                var installOptions = options ?? new ExtractOptions();
                if (string.IsNullOrEmpty(installOptions.CustomInstallName))
                {
                    installOptions.CustomInstallName = GenerateInstallNameForRelease(release, asset);
                }
                
                // Set archive path to downloaded file path
                installOptions.ArchivePath = downloadedFilePath;

                // Create a GitHubArtifact from the release info for metadata
                var pseudoArtifact = new GitHubArtifact
                {
                    Id = asset.Id, // Fix: Use the ID directly as a long instead of converting to string
                    Name = asset.Name,
                    IsRelease = true,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    SizeInBytes = asset.Size,
                    CreatedAt = asset.CreatedAt,
                    Expired = false,
                    RepositoryInfo = new GitHubRepository // Fix: Use GitHubRepository instead of RepositoryInfo
                    {
                        // Fix: Access repository information correctly based on available properties
                        RepoOwner = release.Repository?.RepoOwner ?? "Unknown",
                        RepoName = release.Repository?.RepoName ?? "Unknown",
                        DisplayName = release.Repository?.DisplayName ?? release.Name
                    }
                };
                
                // Use the standard version install method with our release metadata
                return await InstallVersionAsync(
                    pseudoArtifact,
                    downloadedFilePath, 
                    installOptions,
                    progress,
                    cancellationToken);
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
        /// Generates a directory name for installation based on GitHub Release and Asset properties.
        /// </summary>
        private string GenerateInstallNameForRelease(GitHubRelease release, GitHubReleaseAsset asset)
        {
            // Use release tag and asset name for uniqueness, fallback to dates/IDs if names are generic
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            
            // Check if PublishedAt has a value and use it
            if (release.PublishedAt.HasValue)
            {
                datePart = release.PublishedAt.Value.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            }

            string releaseIdentifier = !string.IsNullOrEmpty(release.TagName) ?
                CleanupVersionName(release.TagName) :
                $"Release_{release.Id}";

            string assetIdentifier = CleanupVersionName(asset.Name);

            // Ensure assetIdentifier is distinct if it's a common name like 'archive.zip'
            if (assetIdentifier.Equals("archive", StringComparison.OrdinalIgnoreCase) ||
                assetIdentifier.Equals("source_code", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(assetIdentifier).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(assetIdentifier).Equals(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                assetIdentifier = $"{assetIdentifier}_{asset.Id}";
            }

            return $"{datePart}_{releaseIdentifier}_{assetIdentifier}";
        }

        /// <summary>
        /// Cleans up a name for use as a version name
        /// </summary>
        private string CleanupVersionName(string name)
        {
            // Remove invalid file path characters
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegex = string.Format(@"[{0}]", invalidChars);
            return Regex.Replace(name, invalidRegex, "_");
        }

        /// <summary>
        /// Uninstalls a game version
        /// </summary>
        public async Task<OperationResult> UninstallVersionAsync(string versionId)
        {
            try
            {
                // Get the version
                var version = await _versionRepository.GetByIdAsync(versionId);
                if (version == null)
                {
                    _logger.LogWarning("Cannot uninstall version with ID {VersionId} - not found", versionId);
                    return OperationResult.Failed($"Version with ID {versionId} not found");
                }

                // Make sure we have a valid installation path
                if (string.IsNullOrEmpty(version.InstallPath) || !Directory.Exists(version.InstallPath))
                {
                    _logger.LogWarning("Cannot uninstall version {VersionName} - invalid install path", version.Name);

                    // Delete from repository even if directory doesn't exist
                    await _versionRepository.DeleteAsync(versionId);
                    return OperationResult.Succeeded();
                }

                // Delete the installation directory
                Directory.Delete(version.InstallPath, true);

                // Delete from repository
                await _versionRepository.DeleteAsync(versionId);

                _logger.LogInformation("Successfully uninstalled version {VersionName}", version.Name);
                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uninstalling version {VersionId}: {ErrorMessage}", versionId, ex.Message);
                return OperationResult.Failed($"Failed to uninstall version: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a GameVersion object from a GitHub artifact.
        /// </summary>
        private GameVersion CreateGameVersionFromArtifact(GitHubArtifact artifact, string installPath, string executablePath, string dataPath)
        {
            var gameType = DetermineGameTypeFromArtifact(artifact);
            var isZeroHour = IsZeroHourVariant(artifact);
            
            var version = new GameVersion
            {
                Id = Guid.NewGuid().ToString(),
                Name = artifact.Name,
                Description = artifact.GetDisplayName(),
                InstallPath = installPath,
                GamePath = Path.GetDirectoryName(executablePath) ?? installPath,
                ExecutablePath = executablePath,
                SourceType = GameInstallationType.GitHubArtifact,
                GameType = gameType,
                InstallDate = DateTime.UtcNow,
                IsValid = true,
                BuildDate = artifact.CreatedAt,
                IsZeroHour = isZeroHour,
                // Add GitHub-specific metadata
                SourceSpecificMetadata = new GitHubSourceMetadata
                {
                    AssociatedArtifact = artifact.CreateCopy(),
                    WorkflowDefinitionName = artifact.Name,
                    WorkflowRunStatus = "completed" // Default assumption
                }
            };

            return version;
        }

        /// <summary>
        /// Helper methods to determine game type and variant
        /// </summary>
        private string DetermineGameTypeFromArtifact(GitHubArtifact artifact)
        {
            // First check build info if available
            if (artifact.BuildInfo?.GameVariant == GameVariant.ZeroHour)
                return "ZeroHour";
                
            if (artifact.BuildInfo?.GameVariant == GameVariant.Generals)
                return "Generals";
                
            // Otherwise try to determine from the name
            string name = artifact.Name.ToLowerInvariant();
            
            if (name.Contains("zerohour") || name.Contains("zero hour") || name.Contains("zh"))
                return "ZeroHour";
                
            // Default to Generals
            return "Generals";
        }
        
        private bool IsZeroHourVariant(GitHubArtifact artifact)
        {
            // Check build info first
            if (artifact.BuildInfo?.GameVariant == GameVariant.ZeroHour)
                return true;
                
            // Check name as fallback
            string name = artifact.Name.ToLowerInvariant();
            return name.Contains("zerohour") || name.Contains("zero hour") || name.Contains("zh");
        }

        /// <summary>
        /// Creates metadata for an installed version
        /// </summary>
        private void CreateMetadataForVersion(GameVersion version, string installDir)
        {
            try
            {
                _logger.LogInformation("Creating metadata for installed version in {InstallDir}", installDir);

                // Skip if version is null
                if (version == null)
                {
                    _logger.LogWarning("Cannot create metadata: version is null");
                    return;
                }

                // Create a JSON file with the same name as the directory
                string dirName = Path.GetFileName(installDir);
                string jsonPath = Path.Combine(installDir, $"{dirName}.json");

                // Set directory as install path
                version.InstallPath = installDir;
                
                // For GitHub installations, mark any installed executables as GitHub source type
                if (version.SourceType == GameInstallationType.GitHubArtifact || 
                    version.SourceType == GameInstallationType.GitHubRelease ||
                    version.GitHubMetadata != null)
                {
                    // Look for the modified executables
                    var githubExes = Directory.GetFiles(installDir, "*.exe")
                        .Where(file => {
                            string fileName = Path.GetFileName(file).ToLowerInvariant();
                            return fileName == "generalsv.exe" || fileName == "generalszh.exe";
                        })
                        .ToList();
                    
                    if (githubExes.Any())
                    {
                        // Set the executable path to the first GitHub executable found
                        version.ExecutablePath = githubExes[0];
                    }
                }

                // Save the version to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(version, options);
                File.WriteAllText(jsonPath, json);

                _logger.LogInformation("Created metadata file at {JsonPath}", jsonPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating metadata for version in {InstallDir}", installDir);
            }
        }

        /// <summary>
        /// Installs a game version - facade compatibility method
        /// </summary>
        public async Task<OperationResult> InstallVersionAsync(
            GameVersion version,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            try
            {
                if (version == null)
                {
                    return OperationResult.Failed("Game version cannot be null");
                }

                _logger.LogInformation("Installing game version: {VersionName} (ID: {VersionId})", version.Name, version.Id);

                // Convert string progress to InstallProgress if provided
                IProgress<InstallProgress>? installProgress = null;
                if (progress != null)
                {
                    installProgress = new Progress<InstallProgress>(ip => progress.Report(ip.Message ?? ip.Stage.ToString()));
                }

                // If this is a GitHub artifact, try to install from artifact metadata
                if (version.SourceSpecificMetadata is GitHubSourceMetadata githubMetadata && 
                    githubMetadata.AssociatedArtifact != null)
                {
                    // We need a zip path - check if there's a cached download or temp file
                    string? zipPath = null;
                    
                    // Try to find existing installation or download
                    if (!string.IsNullOrEmpty(version.InstallPath) && Directory.Exists(version.InstallPath))
                    {
                        // Already installed, just register it
                        await _versionRepository.AddAsync(version, cancellationToken);
                        return OperationResult.Succeeded();
                    }

                    if (string.IsNullOrEmpty(zipPath))
                    {
                        return OperationResult.Failed("GitHub artifact installation requires a zip file path");
                    }

                    var result = await InstallVersionAsync(
                        githubMetadata.AssociatedArtifact,
                        zipPath,
                        version.Options,
                        installProgress,
                        cancellationToken);

                    return result.Success ? OperationResult.Succeeded() : OperationResult.Failed(result.Message ?? "Installation failed");
                }
                else if (!string.IsNullOrEmpty(version.InstallPath) && File.Exists(version.InstallPath) && 
                         Path.GetExtension(version.InstallPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // Install from local zip file
                    var result = await InstallVersionFromZipAsync(
                        version.InstallPath,
                        version.Options,
                        installProgress,
                        cancellationToken);

                    return result.Success ? OperationResult.Succeeded() : OperationResult.Failed(result.Message ?? "Installation failed");
                }
                else
                {
                    // Simple registration of existing installation
                    if (!string.IsNullOrEmpty(version.InstallPath) && Directory.Exists(version.InstallPath))
                    {
                        await _versionRepository.AddAsync(version, cancellationToken);
                        return OperationResult.Succeeded();
                    }
                    
                    return OperationResult.Failed("Cannot install version: no valid installation source found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing game version: {VersionId}", version?.Id ?? "Unknown");
                return OperationResult.Failed($"Installation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Uninstalls a game version - simplified interface for facade
        /// </summary>
        public async Task<OperationResult> UninstallVersionAsync(string versionId, CancellationToken cancellationToken = default)
        {
            return await UninstallVersionAsync(versionId);
        }
    }
}
