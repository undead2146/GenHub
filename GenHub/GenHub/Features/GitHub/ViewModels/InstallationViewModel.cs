using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Github;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Storage;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// ViewModel for handling GitHub artifact/release installation.
/// </summary>
public partial class InstallationViewModel(
    IGitHubServiceFacade gitHubService,
    ICasService casService,
    IContentStorageService contentStorageService,
    IContentManifestBuilder manifestBuilder,
    IGitHubContentProcessor gitHubContentProcessor,
    IContentOrchestrator contentOrchestrator,
    IContentManifestPool contentManifestPool,
    IDependencyResolver dependencyResolver,
    ILogger<InstallationViewModel> logger) : ObservableObject
{
    private readonly IGitHubServiceFacade _gitHubService = gitHubService;
    private readonly ICasService _casService = casService;
    private readonly IContentStorageService _contentStorageService = contentStorageService;
    private readonly IContentManifestBuilder _manifestBuilder = manifestBuilder;
    private readonly IGitHubContentProcessor _gitHubContentProcessor = gitHubContentProcessor;
    private readonly IContentOrchestrator _contentOrchestrator = contentOrchestrator;
    private readonly IContentManifestPool _contentManifestPool = contentManifestPool;
    private readonly IDependencyResolver _dependencyResolver = dependencyResolver;
    private readonly ILogger<InstallationViewModel> _logger = logger;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedItemCommand))]
    private bool isInstalling;

    [ObservableProperty]
    private double installationProgress;

    [ObservableProperty]
    private string statusMessage = GitHubConstants.ReadyToInstallMessage;

    [ObservableProperty]
    private object? currentItem;

    [ObservableProperty]
    private GitHubDisplayItemViewModel? selectedItem;

    [ObservableProperty]
    private ContentType selectedContentType = ContentType.Mod;

    /// <summary>
    /// Gets the available content types for selection.
    /// </summary>
    public ContentType[] AvailableContentTypes { get; } = new[]
    {
        ContentType.GameClient,
        ContentType.Mod,
        ContentType.Patch,
        ContentType.Addon,
        ContentType.MapPack,
        ContentType.LanguagePack,
        ContentType.Mission,
        ContentType.Map,
        ContentType.ContentBundle,
    };

    /// <summary>
    /// Gets the installation log entries.
    /// </summary>
    public ObservableCollection<string> InstallationLog { get; } = new();

    /// <summary>
    /// Gets a value indicating whether an item can be installed.
    /// </summary>
    public bool CanInstallItem => SelectedItem != null && !IsInstalling && SelectedItem.CanInstall;

    /// <summary>
    /// Gets a value indicating whether an operation is active.
    /// </summary>
    public bool IsOperationActive => IsInstalling;

    /// <summary>
    /// Installs the currently selected item with proper ContentManifest generation and CAS storage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand(CanExecute = nameof(CanInstallItem))]
    public async Task InstallSelectedItemAsync()
    {
        if (SelectedItem == null) return;

        IsInstalling = true;
        InstallationProgress = 0;
        InstallationLog.Clear();
        StatusMessage = "Starting installation...";

        try
        {
            InstallationProgress = 10;
            StatusMessage = "Preparing content for installation...";
            InstallationLog.Add($"Preparing {SelectedItem.DisplayName} for installation...");

            // Create a ContentSearchResult from the selected item
            var searchResult = CreateContentSearchResultFromSelectedItem();
            if (searchResult == null)
            {
                throw new InvalidOperationException($"Cannot create search result for item type: {SelectedItem.GetType().Name}");
            }

            // Resolve dependencies before installation
            InstallationLog.Add("Resolving dependencies...");
            var depsResult = await _dependencyResolver.ResolveDependenciesWithManifestsAsync(
                new[] { searchResult.Id }, CancellationToken.None);

            if (!depsResult.Success || depsResult.HasErrors)
            {
                var errors = string.Join("\n", depsResult.Errors);
                StatusMessage = $"Dependency resolution failed:\n{errors}";
                InstallationLog.Add($"❌ Cannot install: {errors}");
                return;
            }

            // Check for missing dependencies
            if (depsResult.MissingContentIds.Any())
            {
                var missing = string.Join(", ", depsResult.MissingContentIds);
                StatusMessage = $"Missing required dependencies: {missing}";
                InstallationLog.Add($"❌ Missing dependencies: {missing}");
                return;
            }

            // Check for conflicts (warnings in dependency resolution)
            if (depsResult.Warnings.Any())
            {
                var conflicts = string.Join("\n", depsResult.Warnings);
                StatusMessage = $"Dependency conflicts detected:\n{conflicts}";
                InstallationLog.Add($"⚠️ Conflicts: {conflicts}");

                // For now, we'll proceed with conflicts but log them
                // TODO: Add user dialog to confirm proceeding with conflicts
                InstallationLog.Add("Proceeding despite conflicts...");
            }

            InstallationLog.Add("✅ Dependencies resolved successfully");

            // Use the ContentOrchestrator to acquire content through the proper pipeline
            var progress = new Progress<ContentAcquisitionProgress>(p =>
            {
                InstallationProgress = p.ProgressPercentage;
                StatusMessage = p.CurrentOperation;
                InstallationLog.Add($"{p.CurrentOperation} ({p.ProgressPercentage}%)");
            });

            var result = await _contentOrchestrator.AcquireContentAsync(
                searchResult,
                progress,
                CancellationToken.None);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Content acquisition failed: {result.FirstError}");
            }

            var manifest = result.Data!;
            InstallationProgress = 100;
            StatusMessage = $"Successfully installed {SelectedItem.DisplayName}";
            InstallationLog.Add("Installation completed successfully!");
            InstallationLog.Add($"Content Type: {SelectedContentType}");
            InstallationLog.Add($"Manifest ID: {manifest.Id}");

            // Verify installation
            var verifyResult = await _contentStorageService.IsContentStoredAsync(manifest.Id);
            if (!verifyResult.Success || !verifyResult.Data)
            {
                throw new InvalidOperationException("Content verification failed after installation");
            }

            InstallationLog.Add("✅ Content verification passed");

            // Update UI state
            if (SelectedItem is GitHubArtifactDisplayItemViewModel artifact)
            {
                artifact.IsInstalled = true;
            }

            _logger.LogInformation(
                "Successfully installed {ItemName} with manifest {ManifestId}",
                SelectedItem.DisplayName,
                manifest.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install selected item: {ItemName}", SelectedItem.DisplayName);
            StatusMessage = $"Installation failed: {ex.Message}";
            InstallationLog.Add($"ERROR: {ex.Message}");
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Cancels the current installation.
    /// </summary>
    [RelayCommand]
    public void CancelInstallation()
    {
        StatusMessage = GitHubConstants.InstallationCancelledMessage;
        IsInstalling = false;
        InstallationProgress = 0;
        InstallationLog.Add(GitHubConstants.InstallationCancelledMessage);
    }

    /// <summary>
    /// Installs a GitHub release asset.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="asset">The release asset to install.</param>
    /// <returns>A task representing the installation operation.</returns>
    public async Task InstallReleaseAssetAsync(string owner, string repositoryName, GitHubReleaseAsset asset)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repositoryName) || asset == null)
        {
            StatusMessage = GitHubConstants.InvalidInstallationParametersMessage;
            return;
        }

        try
        {
            IsInstalling = true;
            InstallationProgress = 0;
            CurrentItem = asset;
            InstallationLog.Clear();
            InstallationLog.Add(string.Format(GitHubConstants.StartingReleaseAssetInstallationFormat, asset.Name));

            // Create temp directory for download
            var tempDir = Path.Combine(Path.GetTempPath(), "GenHub", "GitHub", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var tempFilePath = Path.Combine(tempDir, asset.Name);
            InstallationLog.Add($"Downloading to temporary location: {tempFilePath}");

            // Download the asset
            InstallationProgress = 25;
            StatusMessage = GitHubConstants.DownloadingMessage;
            var downloadResult = await gitHubService.DownloadReleaseAssetAsync(owner, repositoryName, asset, tempFilePath);

            if (!downloadResult.Success)
            {
                StatusMessage = string.Format(GitHubConstants.DownloadFailedFormat, downloadResult.ErrorMessage);
                InstallationLog.Add(string.Format(GitHubConstants.DownloadFailedFormat, downloadResult.ErrorMessage));
                return;
            }

            InstallationLog.Add(GitHubConstants.DownloadCompletedSuccessfullyMessage);
            InstallationProgress = 50;
            StatusMessage = GitHubConstants.InstallingMessage;

            // Store in CAS
            var storeResult = await casService.StoreContentAsync(tempFilePath);
            if (!storeResult.Success)
            {
                StatusMessage = string.Format(GitHubConstants.InstallationFailedFormat, storeResult.FirstError);
                InstallationLog.Add(string.Format(GitHubConstants.InstallationFailedFormat, storeResult.FirstError));
                return;
            }

            InstallationLog.Add($"Stored in CAS with hash: {storeResult.Data}");

            InstallationProgress = 100;
            StatusMessage = GitHubConstants.InstallationCompletedSuccessfullyMessage;
            InstallationLog.Add(GitHubConstants.InstallationCompletedMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install release asset {AssetId}", asset.Id);
            StatusMessage = string.Format(GitHubConstants.InstallationFailedFormat, ex.Message);
            InstallationLog.Add(string.Format(GitHubConstants.InstallationFailedFormat, ex.Message));
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Installs a GitHub artifact.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <param name="artifact">The artifact to install.</param>
    /// <returns>A task representing the installation operation.</returns>
    public async Task InstallArtifactAsync(string owner, string repositoryName, GitHubArtifact artifact)
    {
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repositoryName) || artifact == null)
        {
            StatusMessage = GitHubConstants.InvalidInstallationParametersMessage;
            return;
        }

        try
        {
            IsInstalling = true;
            InstallationProgress = 0;
            CurrentItem = artifact;
            InstallationLog.Clear();
            InstallationLog.Add(string.Format(GitHubConstants.StartingArtifactInstallationFormat, artifact.Name));

            // Create temp directory for download
            var tempDir = Path.Combine(Path.GetTempPath(), "GenHub", "GitHub", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var tempFilePath = Path.Combine(tempDir, $"{artifact.Name}.zip");
            InstallationLog.Add($"Downloading to temporary location: {tempFilePath}");

            // Download the artifact
            InstallationProgress = 25;
            StatusMessage = GitHubConstants.DownloadingMessage;
            var downloadResult = await gitHubService.DownloadArtifactAsync(owner, repositoryName, artifact, tempFilePath);

            if (!downloadResult.Success)
            {
                StatusMessage = string.Format(GitHubConstants.DownloadFailedFormat, downloadResult.ErrorMessage);
                InstallationLog.Add(string.Format(GitHubConstants.DownloadFailedFormat, downloadResult.ErrorMessage));
                return;
            }

            InstallationLog.Add(GitHubConstants.DownloadCompletedSuccessfullyMessage);
            InstallationProgress = 50;
            StatusMessage = GitHubConstants.InstallingMessage;

            // Store in CAS
            var storeResult = await casService.StoreContentAsync(tempFilePath);
            if (!storeResult.Success)
            {
                StatusMessage = string.Format(GitHubConstants.InstallationFailedFormat, storeResult.FirstError);
                InstallationLog.Add(string.Format(GitHubConstants.InstallationFailedFormat, storeResult.FirstError));
                return;
            }

            InstallationLog.Add($"Stored in CAS with hash: {storeResult.Data}");

            InstallationProgress = 100;
            StatusMessage = GitHubConstants.InstallationCompletedSuccessfullyMessage;
            InstallationLog.Add(GitHubConstants.InstallationCompletedMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install artifact {ArtifactId}", artifact.Id);
            StatusMessage = string.Format(GitHubConstants.InstallationFailedFormat, ex.Message);
            InstallationLog.Add(string.Format(GitHubConstants.InstallationFailedFormat, ex.Message));
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Sets the selected item for installation.
    /// </summary>
    /// <param name="item">The selected item.</param>
    public void SetSelectedItem(GitHubDisplayItemViewModel? item)
    {
        SelectedItem = item;

        if (item != null)
        {
            StatusMessage = $"Ready to install: {item.DisplayName}";

            // Auto-select appropriate content type based on item name/description
            SelectedContentType = InferContentType(item.DisplayName, item.Description);

            // Log item capabilities for debugging
            logger.LogDebug(
                "Selected item: {DisplayName}, CanInstall: {CanInstall}, CanDownload: {CanDownload}, Type: {Type}",
                item.DisplayName,
                item.CanInstall,
                item.CanDownload,
                item.GetType().Name);
        }
        else
        {
            StatusMessage = "Select an item to install";
        }

        // Notify that CanInstallItem may have changed
        InstallSelectedItemCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstallItem));
    }

    /// <summary>
    /// Clears the selected item.
    /// </summary>
    public void ClearSelection()
    {
        SelectedItem = null;
        StatusMessage = GitHubConstants.ReadyToInstallMessage;

        // Cancel any ongoing operation if needed
        if (IsOperationActive)
        {
            CancelInstallationCommand.Execute(null);
        }
    }

    /// <summary>
    /// Gets the owner name from an artifact view model.
    /// </summary>
    /// <param name="artifactVm">The artifact view model.</param>
    /// <returns>The owner name.</returns>
    private static string GetOwnerFromArtifact(GitHubArtifactDisplayItemViewModel artifactVm)
    {
        return artifactVm.Owner;
    }

    /// <summary>
    /// Gets the repository name from an artifact view model.
    /// </summary>
    /// <param name="artifactVm">The artifact view model.</param>
    /// <returns>The repository name.</returns>
    private static string GetRepositoryFromArtifact(GitHubArtifactDisplayItemViewModel artifactVm)
    {
        return artifactVm.Repository;
    }

    /// <summary>
    /// Determines if a file is executable based on its extension.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>True if the file is executable.</returns>
    private static bool IsExecutableFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".exe" || extension == ".msi" || extension == ".bat" || extension == ".cmd";
    }

    /// <summary>
    /// Formats file size for display.
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The formatted size string.</returns>
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    /// <summary>
    /// Downloads release assets to the specified directory.
    /// </summary>
    /// <param name="releaseVm">The release view model.</param>
    /// <param name="targetDir">The target directory.</param>
    /// <returns>The path to the main downloaded file.</returns>
    private async Task<string> DownloadReleaseAssetsAsync(GitHubReleaseDisplayItemViewModel releaseVm, string targetDir)
    {
        var release = releaseVm.Release;
        if (release.Assets == null || !release.Assets.Any())
        {
            throw new InvalidOperationException("Release has no assets to download");
        }

        string mainFilePath = string.Empty;
        foreach (var asset in release.Assets)
        {
            var assetPath = Path.Combine(targetDir, asset.Name);
            var downloadResult = await gitHubService.DownloadReleaseAssetAsync(
                release.Author ?? "unknown",
                "unknown", // We need to get repository name from context
                asset,
                assetPath);

            if (!downloadResult.Success)
            {
                throw new InvalidOperationException($"Failed to download asset {asset.Name}: {downloadResult.ErrorMessage}");
            }

            if (string.IsNullOrEmpty(mainFilePath) || asset.Name.EndsWith(".zip") || asset.Name.EndsWith(".exe"))
            {
                mainFilePath = assetPath;
            }

            InstallationLog.Add($"Downloaded: {asset.Name} ({FormatFileSize(asset.Size)})");
        }

        return mainFilePath;
    }

    /// <summary>
    /// Downloads an artifact to the specified directory.
    /// </summary>
    /// <param name="artifactVm">The artifact view model.</param>
    /// <param name="targetDir">The target directory.</param>
    /// <returns>The path to the downloaded file.</returns>
    private async Task<string> DownloadArtifactAsync(GitHubArtifactDisplayItemViewModel artifactVm, string targetDir)
    {
        var artifactPath = Path.Combine(targetDir, $"{artifactVm.Artifact.Name}.zip");

        // Get repository info from the artifact VM
        var owner = GetOwnerFromArtifact(artifactVm);
        var repository = GetRepositoryFromArtifact(artifactVm);

        var downloadResult = await gitHubService.DownloadArtifactAsync(
            owner,
            repository,
            artifactVm.Artifact,
            artifactPath);

        if (!downloadResult.Success)
        {
            throw new InvalidOperationException($"Failed to download artifact: {downloadResult.ErrorMessage}");
        }

        InstallationLog.Add($"Downloaded: {artifactVm.Artifact.Name} ({FormatFileSize(artifactVm.Artifact.SizeInBytes)})");
        return artifactPath;
    }

    /// <summary>
    /// Creates a ContentManifest for the given item.
    /// </summary>
    /// <param name="item">The GitHub item.</param>
    /// <param name="downloadPath">The path to the downloaded content.</param>
    /// <returns>The created ContentManifest.</returns>
    private async Task<ContentManifest> CreateContentManifestAsync(GitHubDisplayItemViewModel item, string downloadPath)
    {
        var manifestId = new ManifestId(Guid.NewGuid().ToString());
        var fileName = Path.GetFileName(downloadPath);

        var manifest = manifestBuilder
            .WithBasicInfo(manifestId, item.DisplayName, 1)
            .WithContentType(SelectedContentType, GameType.Generals) // Default to C&C Generals for now
            .WithPublisher("GitHub")
            .WithMetadata(
                item.Description ?? string.Empty,
                tags: new[] { "github", "community", SelectedContentType.ToString().ToLowerInvariant() }.ToList(),
                changelogUrl: string.Empty)
            .WithInstallationInstructions(WorkspaceStrategy.HybridCopySymlink);

        // Add the downloaded file to the manifest
        await manifest.AddLocalFileAsync(
            fileName,
            downloadPath,
            ContentSourceType.LocalFile,
            isExecutable: IsExecutableFile(fileName));

        return manifest.Build();
    }

    /// <summary>
    /// Creates a ContentSearchResult from the currently selected item.
    /// </summary>
    /// <returns>The content search result, or null if unsupported.</returns>
    private ContentSearchResult? CreateContentSearchResultFromSelectedItem()
    {
        if (SelectedItem == null) return null;

        var searchResult = new ContentSearchResult
        {
            Name = SelectedItem.DisplayName,
            Description = SelectedItem.Description,
            ContentType = SelectedContentType,
            TargetGame = GameType.Generals, // TODO: Make configurable
            ProviderName = "GitHub",
            IsInferred = true, // Content type is user-selected
            RequiresResolution = true,
            ResolverId = "github-resolver",
        };

        if (SelectedItem is GitHubReleaseDisplayItemViewModel releaseVm)
        {
            searchResult.Id = $"{releaseVm.Owner}/{releaseVm.Repository}/releases/{releaseVm.Release.Id}";
            searchResult.Version = releaseVm.Release.TagName ?? "latest";
            searchResult.SourceUrl = releaseVm.Release.HtmlUrl;
            searchResult.LastUpdated = releaseVm.Release.PublishedAt?.DateTime ?? DateTime.Now;

            // Store the release asset data for the resolver
            var asset = releaseVm.Release.Assets?.FirstOrDefault();
            if (asset != null)
            {
                searchResult.Data = asset;
                searchResult.DownloadSize = asset.Size;
            }

            // Add resolver metadata
            searchResult.ResolverMetadata["owner"] = releaseVm.Owner;
            searchResult.ResolverMetadata["repository"] = releaseVm.Repository;
            searchResult.ResolverMetadata["releaseId"] = releaseVm.Release.Id.ToString();
            searchResult.ResolverMetadata["assetId"] = asset?.Id.ToString() ?? string.Empty;
        }
        else if (SelectedItem is GitHubArtifactDisplayItemViewModel artifactVm)
        {
            searchResult.Id = $"{artifactVm.Owner}/{artifactVm.Repository}/artifacts/{artifactVm.Artifact.Id}";
            searchResult.Version = artifactVm.Artifact.CreatedAt.ToString("yyyy-MM-dd");
            searchResult.LastUpdated = artifactVm.Artifact.CreatedAt;
            searchResult.DownloadSize = artifactVm.Artifact.SizeInBytes;

            // Store the artifact data for the resolver
            searchResult.Data = artifactVm.Artifact;

            // Add resolver metadata
            searchResult.ResolverMetadata["owner"] = artifactVm.Owner;
            searchResult.ResolverMetadata["repository"] = artifactVm.Repository;
            searchResult.ResolverMetadata["artifactId"] = artifactVm.Artifact.Id.ToString();
            searchResult.ResolverMetadata["workflowRunId"] = artifactVm.Artifact.WorkflowRun?.Id.ToString() ?? string.Empty;
        }
        else
        {
            return null; // Unsupported item type
        }

        return searchResult;
    }

    /// <summary>
    /// Infers the content type based on the item name and description.
    /// </summary>
    /// <param name="name">The item name.</param>
    /// <param name="description">The item description.</param>
    /// <returns>The inferred content type.</returns>
    private ContentType InferContentType(string name, string? description)
    {
        var text = $"{name} {description}".ToLowerInvariant();

        if (text.Contains("map") && text.Contains("pack")) return ContentType.MapPack;
        if (text.Contains("map")) return ContentType.Map;
        if (text.Contains("mission")) return ContentType.Mission;
        if (text.Contains("patch") || text.Contains("fix") || text.Contains("update")) return ContentType.Patch;
        if (text.Contains("addon") || text.Contains("utility") || text.Contains("tool")) return ContentType.Addon;
        if (text.Contains("language") || text.Contains("translation")) return ContentType.LanguagePack;
        if (text.Contains("bundle") || text.Contains("collection")) return ContentType.ContentBundle;

        return ContentType.Mod; // Default to Mod
    }
}
