using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// View model for the GitHub details view.
/// </summary>
public partial class GitHubDetailsViewModel : ObservableObject
{
    private readonly IGitHubServiceFacade _gitHubService;
    private readonly ILogger<GitHubDetailsViewModel> _logger;

    [ObservableProperty]
    private GitHubDisplayItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _statusMessage = GitHubConstants.SelectItemMessage;

    [ObservableProperty]
    private ObservableCollection<DetailItem> _details = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubDetailsViewModel"/> class.
    /// </summary>
    /// <param name="gitHubService">The GitHub service facade.</param>
    /// <param name="logger">The logger instance.</param>
    public GitHubDetailsViewModel(
        IGitHubServiceFacade gitHubService,
        ILogger<GitHubDetailsViewModel> logger)
    {
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a value indicating whether the download operation can be executed.
    /// </summary>
    public bool CanDownload => SelectedItem?.CanDownload == true && !IsLoading;

    /// <summary>
    /// Gets a value indicating whether the install operation can be executed.
    /// </summary>
    public bool CanInstall => SelectedItem?.CanInstall == true && !IsLoading;

    /// <summary>
    /// Sets the selected item and loads its details.
    /// </summary>
    /// <param name="item">The item to select.</param>
    public void SetSelectedItem(GitHubDisplayItemViewModel? item)
    {
        SelectedItem = item;
        Details.Clear();

        if (item == null)
        {
            StatusMessage = GitHubConstants.SelectItemMessage;
            return;
        }

        // Load details for the selected item
        LoadItemDetails(item);
        StatusMessage = string.Format(GitHubConstants.ViewingDetailsFormat, item.DisplayName);
        _logger.LogDebug("Set selected item: {DisplayName}", item.DisplayName);
    }

    /// <summary>
    /// Clears the selected item and resets the details view.
    /// </summary>
    [RelayCommand]
    public void ClearSelection()
    {
        SelectedItem = null;
        Details.Clear();
        StatusMessage = GitHubConstants.SelectItemMessage;
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (SelectedItem == null)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = GitHubConstants.DownloadingMessage;

        try
        {
            await SelectedItem.DownloadAsync();
            StatusMessage = GitHubConstants.DownloadCompletedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download item {DisplayName}", SelectedItem.DisplayName);
            StatusMessage = string.Format(GitHubConstants.DownloadFailedFormat, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (SelectedItem == null)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = GitHubConstants.InstallingMessage;

        try
        {
            await SelectedItem.InstallAsync();
            StatusMessage = GitHubConstants.InstallationCompletedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install item {DisplayName}", SelectedItem.DisplayName);
            StatusMessage = string.Format(GitHubConstants.InstallationFailedFormat, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadItemDetails(GitHubDisplayItemViewModel item)
    {
        // Add basic information
        Details.Add(new DetailItem(GitHubConstants.NameLabel, item.DisplayName));
        Details.Add(new DetailItem(GitHubConstants.DescriptionLabel, item.Description));
        Details.Add(new DetailItem(GitHubConstants.TypeLabel, GetItemTypeDisplayName(item)));

        // Format date properly, handling default DateTime values
        var dateDisplay = item.SortDate == DateTime.MinValue || item.SortDate == default(DateTime)
            ? "Not available"
            : item.SortDate.ToString("MMM dd, yyyy HH:mm");
        Details.Add(new DetailItem(GitHubConstants.DateLabel, dateDisplay));

        // Add type-specific details
        if (item.IsRelease)
        {
            Details.Add(new DetailItem(GitHubConstants.ReleaseTypeLabel, GitHubConstants.ReleaseItemType));
        }
        else if (item.IsWorkflowRun)
        {
            Details.Add(new DetailItem(GitHubConstants.WorkflowTypeLabel, GitHubConstants.WorkflowRunItemType));
            if (item.RunNumber.HasValue)
            {
                Details.Add(new DetailItem(GitHubConstants.RunNumberLabel, item.RunNumber.Value.ToString()));
            }
        }

        // Add action capabilities
        Details.Add(new DetailItem(GitHubConstants.CanDownloadLabel, item.CanDownload ? GitHubConstants.CapabilityYes : GitHubConstants.CapabilityNo));
        Details.Add(new DetailItem(GitHubConstants.CanInstallLabel, item.CanInstall ? GitHubConstants.CapabilityYes : GitHubConstants.CapabilityNo));
    }

    private string GetItemTypeDisplayName(GitHubDisplayItemViewModel item)
    {
        if (item.IsRelease)
        {
            return GitHubConstants.ReleaseItemType;
        }

        if (item.IsWorkflowRun)
        {
            return GitHubConstants.WorkflowRunItemType;
        }

        return GitHubConstants.UnknownItemType;
    }
}

/// <summary>
/// Simple detail item for display in the details view.
/// </summary>
public class DetailItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DetailItem"/> class.
    /// </summary>
    /// <param name="label">The label for the detail item.</param>
    /// <param name="value">The value for the detail item.</param>
    public DetailItem(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>
    /// Gets the label for the detail item.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the value for the detail item.
    /// </summary>
    public string Value { get; }
}
