using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels.Items;

/// <summary>
/// View model for a GitHub release.
/// </summary>
public partial class GitHubReleaseDisplayItemViewModel : GitHubDisplayItemViewModel
{
    private readonly IGitHubServiceFacade _gitHubService;
    private readonly string _owner;
    private readonly string _repository;

    [ObservableProperty]
    private bool _isLoadingChildren;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isInstalling;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleaseDisplayItemViewModel"/> class.
    /// </summary>
    /// <param name="release">The GitHub release to display.</param>
    /// <param name="gitHubService">The GitHub service facade.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="logger">The logger instance.</param>
    public GitHubReleaseDisplayItemViewModel(
        GitHubRelease release,
        IGitHubServiceFacade gitHubService,
        string owner,
        string repository,
        ILogger logger)
        : base(logger)
    {
        Release = release ?? throw new ArgumentNullException(nameof(release));
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
        _owner = owner;
        _repository = repository;
    }

    /// <summary>
    /// Gets the underlying GitHub release.
    /// </summary>
    public GitHubRelease Release { get; }

    /// <summary>
    /// Gets the repository owner.
    /// </summary>
    public string Owner => _owner;

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string Repository => _repository;

    /// <summary>
    /// Gets the display name for the release.
    /// </summary>
    public override string DisplayName => Release.TagName ?? Release.Name ?? "Unknown Release";

    /// <summary>
    /// Gets the description for the release.
    /// </summary>
    public override string Description
    {
        get
        {
            var assetCount = Release.Assets?.Count ?? 0;
            var publishedDate = Release.PublishedAt?.ToString("MMM dd, yyyy") ?? "Unknown date";
            return $"{Release.Name} - {assetCount} assets - Published {publishedDate}";
        }
    }

    /// <summary>
    /// Gets a value indicating whether the release is expandable.
    /// </summary>
    public override bool IsExpandable => true;

    /// <summary>
    /// Gets the sort date for the release.
    /// </summary>
    public override DateTime SortDate => Release.PublishedAt?.DateTime ?? Release.CreatedAt.DateTime;

    /// <summary>
    /// Gets a value indicating whether this is a release item.
    /// </summary>
    public override bool IsRelease => true;

    /// <summary>
    /// Gets a value indicating whether the release can be downloaded.
    /// </summary>
    public override bool CanDownload => !IsDownloading && !IsInstalling && Release.Assets?.Any() == true;

    /// <summary>
    /// Gets a value indicating whether the release can be installed.
    /// </summary>
    public override bool CanInstall => !IsInstalling && !IsDownloading && Release.Assets?.Any() == true;

    /// <summary>
    /// Toggles the expanded state of the release view model.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    public override async Task ToggleExpandedAsync()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            Children.Clear();
        }
        else
        {
            await LoadChildrenAsync();
            IsExpanded = true;
        }
    }

    /// <summary>
    /// Loads the child artifacts for the release asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task representing the operation.</returns>
    public override async Task LoadChildrenAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoadingChildren || Children.Any()) return;

        IsLoadingChildren = true;
        try
        {
            // Create artifact view models from release assets
            if (Release.Assets != null)
            {
                foreach (var asset in Release.Assets)
                {
                    var artifact = new GitHubArtifact
                    {
                        Name = asset.Name,
                        SizeInBytes = asset.Size,
                        DownloadUrl = asset.BrowserDownloadUrl,
                        CreatedAt = asset.CreatedAt.DateTime,
                        IsInstalled = false, // TODO: Check if already installed
                    };

                    var artifactVm = new GitHubArtifactDisplayItemViewModel(
                        artifact,
                        _gitHubService,
                        _owner,
                        _repository,
                        Logger);

                    Children.Add(artifactVm);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load release assets for {ReleaseName}", Release.Name);
        }
        finally
        {
            IsLoadingChildren = false;
        }
    }

    /// <summary>
    /// Downloads the release assets to the user's Downloads folder.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    public override async Task DownloadAsync()
    {
        if (!CanDownload) return;

        IsDownloading = true;
        try
        {
            var downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "GenHub");
            Directory.CreateDirectory(downloadsDir);

            if (Release.Assets != null)
            {
                foreach (var asset in Release.Assets)
                {
                    var destinationPath = Path.Combine(downloadsDir, asset.Name);
                    await _gitHubService.DownloadReleaseAssetAsync(_owner, _repository, asset, destinationPath);
                    Logger.LogInformation("Downloaded release asset {Name} to {Path}", asset.Name, destinationPath);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to download release {Name}", Release.Name);
            throw;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    /// <summary>
    /// Installs the release by downloading and installing the assets.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    public override async Task InstallAsync()
    {
        if (!CanInstall) return;

        IsInstalling = true;
        try
        {
            await DownloadAsync(); // Download first
            Logger.LogInformation("Installed release: {Name}", Release.Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to install release {Name}", Release.Name);
            throw;
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
