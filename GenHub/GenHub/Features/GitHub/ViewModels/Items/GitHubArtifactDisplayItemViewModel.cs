using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels.Items;

/// <summary>
/// View model for a GitHub workflow artifact.
/// </summary>
public partial class GitHubArtifactDisplayItemViewModel : GitHubDisplayItemViewModel
{
    private readonly IGitHubServiceFacade _gitHubService;
    private readonly string _owner;
    private readonly string _repository;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isInstalled;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubArtifactDisplayItemViewModel"/> class.
    /// </summary>
    /// <param name="artifact">The GitHub artifact to display.</param>
    /// <param name="gitHubService">The GitHub service facade.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="logger">The logger instance.</param>
    public GitHubArtifactDisplayItemViewModel(
        GitHubArtifact artifact,
        IGitHubServiceFacade gitHubService,
        string owner,
        string repository,
        ILogger logger)
        : base(logger)
    {
        Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        IsInstalled = artifact.IsInstalled;
    }

    /// <summary>
    /// Gets the underlying GitHub artifact.
    /// </summary>
    public GitHubArtifact Artifact { get; }

    /// <summary>
    /// Gets the repository owner.
    /// </summary>
    public string Owner => _owner;

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public string Repository => _repository;

    /// <summary>
    /// Gets the display name of the artifact.
    /// </summary>
    public override string DisplayName => Artifact.Name ?? "Unknown Artifact";

    /// <summary>
    /// Gets the description of the artifact.
    /// </summary>
    public override string Description
    {
        get
        {
            var sizeText = FormatFileSize(Artifact.SizeInBytes);
            var dateText = Artifact.CreatedAt == DateTime.MinValue || Artifact.CreatedAt == default(DateTime)
                ? "Unknown date"
                : Artifact.CreatedAt.ToString("g");
            return $"{sizeText} - Created {dateText}";
        }
    }

    /// <summary>
    /// Gets a value indicating whether this artifact is expandable.
    /// </summary>
    public override bool IsExpandable => false;

    /// <summary>
    /// Gets the sort date for the artifact.
    /// </summary>
    public override DateTime SortDate => Artifact.CreatedAt;

    /// <summary>
    /// Gets a value indicating whether this is a release.
    /// </summary>
    public override bool IsRelease => false;

    /// <summary>
    /// Gets a value indicating whether this artifact can be downloaded.
    /// </summary>
    public override bool CanDownload => !IsDownloading && !IsInstalling;

    /// <summary>
    /// Gets a value indicating whether this artifact can be installed.
    /// </summary>
    public override bool CanInstall => !IsInstalling && !IsDownloading && !IsInstalled;

    /// <summary>
    /// Loads child items for this artifact (artifacts don't have children).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task LoadChildrenAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Downloads the artifact asynchronously.
    /// </summary>
    /// <returns>The task representing the download operation.</returns>
    public override async Task DownloadAsync()
    {
        if (!CanDownload) return;

        IsDownloading = true;
        try
        {
            var downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "GenHub");
            Directory.CreateDirectory(downloadsDir);
            var destinationPath = Path.Combine(downloadsDir, $"{Artifact.Name}.zip");

            await _gitHubService.DownloadArtifactAsync(_owner, _repository, Artifact, destinationPath);
            Logger.LogInformation("Downloaded artifact {Name} to {Path}", Artifact.Name, destinationPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to download artifact {Name}", Artifact.Name);
            throw;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    /// <summary>
    /// Installs the artifact asynchronously.
    /// </summary>
    /// <returns>The task representing the installation operation.</returns>
    public override async Task InstallAsync()
    {
        if (!CanInstall) return;

        IsInstalling = true;
        try
        {
            await DownloadAsync(); // Download first
            IsInstalled = true;
            Logger.LogInformation("Installed artifact: {Name}", Artifact.Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to install artifact {Name}", Artifact.Name);
            throw;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }
}
