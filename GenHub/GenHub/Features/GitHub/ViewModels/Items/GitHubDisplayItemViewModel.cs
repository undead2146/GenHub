using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels.Items;

/// <summary>
/// Base class for all GitHub display items with common functionality.
/// </summary>
public abstract partial class GitHubDisplayItemViewModel : ObservableObject
{
    private readonly ILogger _loggerField;

    [ObservableProperty]
    private ObservableCollection<GitHubDisplayItemViewModel> _children = new();

    [ObservableProperty]
    private bool _isExpanded = false;

    [ObservableProperty]
    private bool _childrenLoaded = false;

    [ObservableProperty]
    private bool _isSelected = false;

    [ObservableProperty]
    private bool _isLoadingChildren = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubDisplayItemViewModel"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    protected GitHubDisplayItemViewModel(ILogger logger)
    {
        _loggerField = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the display name for the item.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the description for the item.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the sort date for the item.
    /// </summary>
    public abstract DateTime SortDate { get; }

    /// <summary>
    /// Gets a value indicating whether the item is expandable.
    /// </summary>
    public abstract bool IsExpandable { get; }

    /// <summary>
    /// Gets a value indicating whether the item is a release.
    /// </summary>
    public abstract bool IsRelease { get; }

    /// <summary>
    /// Gets the run number if applicable.
    /// </summary>
    public virtual int? RunNumber => null;

    /// <summary>
    /// Gets a value indicating whether the item can be downloaded.
    /// </summary>
    public virtual bool CanDownload => false;

    /// <summary>
    /// Gets a value indicating whether the item can be installed.
    /// </summary>
    public virtual bool CanInstall => false;

    /// <summary>
    /// Gets a value indicating whether the item is a workflow run.
    /// </summary>
    public virtual bool IsWorkflowRun => false;

    /// <summary>
    /// Gets a value indicating whether the item is active.
    /// </summary>
    public virtual bool IsActive => false;

    /// <summary>
    /// Gets the icon source for the item.
    /// </summary>
    public virtual string? IconSource => null;

    /// <summary>
    /// Gets the logger instance for derived classes.
    /// </summary>
    protected ILogger Logger => _loggerField;

    /// <summary>
    /// Downloads the item asynchronously.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    public virtual async Task DownloadAsync()
    {
        // Base implementation does nothing
        await Task.CompletedTask;
    }

    /// <summary>
    /// Installs the item asynchronously.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    [RelayCommand(CanExecute = nameof(CanInstall))]
    public virtual async Task InstallAsync()
    {
        // Base implementation does nothing
        await Task.CompletedTask;
    }

    /// <summary>
    /// Toggles the expanded state of the item.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    [RelayCommand(CanExecute = nameof(IsExpandable))]
    public virtual async Task ToggleExpandedAsync()
    {
        IsExpanded = !IsExpanded;
        if (IsExpanded && !Children.Any())
        {
            await LoadChildrenAsync();
        }
    }

    /// <summary>
    /// Loads the children of the item asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task representing the operation.</returns>
    public abstract Task LoadChildrenAsync(CancellationToken cancellationToken = default);
}
