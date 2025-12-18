using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Helpers;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.GameProfiles;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.GameProfile;
using GenHub.Features.Content.ViewModels;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Downloads.ViewModels;

/// <summary>
/// ViewModel for a publisher card displaying content from a single publisher.
/// </summary>
public partial class PublisherCardViewModel : ObservableObject, IRecipient<ProfileCreatedMessage>, IRecipient<ProfileDeletedMessage>, IDisposable
{
    private readonly ILogger<PublisherCardViewModel> _logger;
    private readonly IContentOrchestrator _contentOrchestrator;
    private readonly IContentManifestPool _manifestPool;
    private readonly IProfileContentService? _profileContentService;
    private readonly IGameProfileManager? _profileManager;
    private readonly INotificationService? _notificationService;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _profileLock = new(1, 1);

    private bool _disposed;

    [ObservableProperty]
    private string _publisherId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _iconPathData = string.Empty;

    [ObservableProperty]
    private string? _logoSource;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private DateTime? _releaseDate;

    [ObservableProperty]
    private long? _downloadSize;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ContentTypeGroup> _contentTypes = new();

    [ObservableProperty]
    private bool _showContentSummary = true;

    [ObservableProperty]
    private string _primaryActionText = string.Empty;

    [ObservableProperty]
    private ICommand? _primaryActionCommand;

    /// <summary>
    /// Gets or sets the available profiles for the "Add to Profile" dropdown.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<GameProfile> _availableProfiles = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PublisherCardViewModel"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="contentOrchestrator">The content orchestrator.</param>
    /// <param name="manifestPool">The manifest pool.</param>
    /// <param name="profileContentService">The profile content service.</param>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="notificationService">The notification service.</param>
    public PublisherCardViewModel(
        ILogger<PublisherCardViewModel> logger,
        IContentOrchestrator contentOrchestrator,
        IContentManifestPool manifestPool,
        IProfileContentService? profileContentService = null,
        IGameProfileManager? profileManager = null,
        INotificationService? notificationService = null)
    {
        _logger = logger;
        _contentOrchestrator = contentOrchestrator;
        _manifestPool = manifestPool;
        _profileContentService = profileContentService;
        _profileManager = profileManager;
        _notificationService = notificationService;

        // Subscribe to collection changes to update HasContent and ContentSummary
        ContentTypes.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(ContentSummary));
        };

        // Register for profile creation and deletion messages
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _cts.Dispose();
            _profileLock.Dispose();
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _disposed = true;
        }
    }

    /// <summary>
    /// Refreshes the list of available profiles.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshAvailableProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (_profileManager == null)
        {
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var token = linkedCts.Token;

        await _profileLock.WaitAsync(token);
        try
        {
            var profilesResult = await _profileManager.GetAllProfilesAsync();
            if (profilesResult.Success && profilesResult.Data != null)
            {
                AvailableProfiles.Clear();
                foreach (var profile in profilesResult.Data)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    AvailableProfiles.Add(profile);
                }

                _logger.LogDebug("Refreshed available profiles: {Count} profiles", AvailableProfiles.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh available profiles");
        }
        finally
        {
            _profileLock.Release();
        }
    }

    /// <summary>
    /// Receives profile creation messages and refreshes the available profiles dropdown.
    /// </summary>
    /// <param name="message">The profile created message.</param>
    public void Receive(ProfileCreatedMessage message)
    {
        _logger.LogDebug("Profile created: {Name}", message.Profile.Name);
        RefreshProfilesOnUiThread();
    }

    /// <summary>
    /// Receives profile deletion messages and refreshes the available profiles dropdown.
    /// </summary>
    /// <param name="message">The profile deleted message.</param>
    public void Receive(ProfileDeletedMessage message)
    {
        _logger.LogDebug("Profile deleted: {Name}", message.ProfileName);
        RefreshProfilesOnUiThread();
    }

    /// <summary>
    /// Called when IsExpanded changes - refresh profiles for the dropdown.
    /// </summary>
    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
        {
            _ = RefreshAvailableProfilesAsync();
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is any content available.
    /// </summary>
    public bool HasContent => ContentTypes?.Count > 0;

    /// <summary>
    /// Gets a summary of available content types (e.g., "2 Content", "3 Content").
    /// </summary>
    public string ContentSummary
    {
        get
        {
            if (ContentTypes == null || ContentTypes.Count == 0)
            {
                return string.Empty;
            }

            var totalCount = ContentTypes.Sum(g => g.Items.Count);

            if (totalCount == 0)
            {
                return string.Empty;
            }

            return totalCount == 1 ? "1 Content" : $"{totalCount} Content";
        }
    }

    /// <summary>
    /// Refreshes the installation status of all content items by checking the manifest pool.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshInstallationStatusAsync()
    {
        try
        {
            var result = await _manifestPool.GetAllManifestsAsync();
            if (!result.Success || result.Data == null)
            {
                _logger.LogWarning("Failed to retrieve manifests for installation status check");
                return;
            }

            // Get all installed manifests
            var allManifests = result.Data.ToList();

            _logger.LogInformation(
                "Checking installation status for {PublisherId} - {ManifestCount} total manifests in pool",
                PublisherId,
                allManifests.Count);

            // Log all manifests for debugging
            foreach (var m in allManifests)
            {
                _logger.LogDebug(
                    "Installed manifest: Id={Id}, Version={Version}, Publisher={Publisher}, ContentType={ContentType}",
                    m.Id.Value,
                    m.Version,
                    m.Publisher?.PublisherType ?? "null",
                    m.ContentType);
            }

            // Update each content item's IsInstalled status
            foreach (var group in ContentTypes)
            {
                foreach (var item in group.Items)
                {
                    var isDownloaded = IsContentInstalledSimple(item, allManifests, PublisherId);
                    item.IsDownloaded = isDownloaded;
                    item.IsInstalled = isDownloaded;

                    _logger.LogDebug(
                        "Content item: {Name} v{Version} ({ContentType}) - Downloaded: {IsDownloaded}",
                        item.Name,
                        item.Version,
                        item.Model.ContentType,
                        item.IsDownloaded);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh installation status for {PublisherId}", PublisherId);
        }
    }

    /// <summary>
    /// Extracts numeric date from version strings like "weekly-2025-11-21" or "20251121".
    /// </summary>
    private static string ExtractDateFromVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return string.Empty;
        }

        var numericVersion = VersionHelper.ExtractVersionFromVersionString(version);
        return numericVersion > 0 ? numericVersion.ToString() : string.Empty;
    }

    /// <summary>
    /// Formats a user-friendly progress status message.
    /// </summary>
    private static string FormatProgressStatus(GenHub.Core.Models.Content.ContentAcquisitionProgress progress)
    {
        var phaseName = progress.Phase switch
        {
            Core.Models.Content.ContentAcquisitionPhase.Downloading => "Downloading",
            Core.Models.Content.ContentAcquisitionPhase.Extracting => "Extracting",
            Core.Models.Content.ContentAcquisitionPhase.Copying => "Copying",
            Core.Models.Content.ContentAcquisitionPhase.ValidatingManifest => "Validating manifest",
            Core.Models.Content.ContentAcquisitionPhase.ValidatingFiles => "Validating files",
            Core.Models.Content.ContentAcquisitionPhase.Delivering => "Installing",
            Core.Models.Content.ContentAcquisitionPhase.Completed => "Complete",
            _ => "Processing",
        };

        // Format with percentage and phase
        var percentText = progress.ProgressPercentage > 0 ? $"{progress.ProgressPercentage:F0}%" : string.Empty;

        // Add file/bytes info if available
        if (progress.TotalBytes > 0 && progress.Phase == Core.Models.Content.ContentAcquisitionPhase.Downloading)
        {
            var downloaded = ByteFormatHelper.FormatBytes(progress.BytesProcessed);
            var total = ByteFormatHelper.FormatBytes(progress.TotalBytes);
            return $"{phaseName}: {downloaded} / {total} ({percentText})";
        }

        if (progress.TotalFiles > 0)
        {
            var phasePercent = progress.TotalFiles > 0
                ? (int)((double)progress.FilesProcessed / progress.TotalFiles * 100)
                : 0;
            return $"{phaseName}: {progress.FilesProcessed}/{progress.TotalFiles} files ({phasePercent}%)";
        }

        if (!string.IsNullOrEmpty(progress.CurrentOperation))
        {
            return $"{phaseName}: {progress.CurrentOperation}";
        }

        return !string.IsNullOrEmpty(percentText) ? $"{phaseName}... {percentText}" : $"{phaseName}...";
    }

    /// <summary>
    /// Simple check if content is installed - matches by manifest ID or by ID components with version.
    /// </summary>
    private static bool IsContentInstalledSimple(
        ContentItemViewModel item,
        List<Core.Models.Manifest.ContentManifest> allManifests,
        string publisherId)
    {
        var itemId = item.Model.Id ?? string.Empty;
        var itemVersion = item.Version ?? string.Empty;
        var itemDatePart = ExtractDateFromVersion(itemVersion);

        foreach (var manifest in allManifests)
        {
            // Direct ID match - most reliable
            if (!string.IsNullOrEmpty(itemId) &&
                manifest.Id.Value.Equals(itemId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // If no direct ID match, try matching by publisher + content type + name
            // Check publisher match by exact ID prefix or type
            var manifestIdParts = manifest.Id.Value.Split('.');
            var hasPublisherInId = manifestIdParts.Length > 2 &&
                manifestIdParts[2].Equals(publisherId, StringComparison.OrdinalIgnoreCase);
            var publisherMatch = hasPublisherInId ||
                (manifest.Publisher?.PublisherType?.Equals(publisherId, StringComparison.OrdinalIgnoreCase) == true);

            if (!publisherMatch)
            {
                continue;
            }

            // Check content type match
            if (manifest.ContentType != item.Model.ContentType)
            {
                continue;
            }

            // For manifests from the same publisher with same content type, check name match
            // This prevents matching all map packs just because they share publisher + type
            var itemName = item.Name?.ToLowerInvariant() ?? string.Empty;
            var manifestName = manifest.Name?.ToLowerInvariant() ?? string.Empty;

            // Skip if names don't match and we have name info
            if (!string.IsNullOrEmpty(itemName) && !string.IsNullOrEmpty(manifestName))
            {
                // Allow for slight variations (e.g., "Far Cry" vs "FarCry")
                var normalizedItemName = itemName.Replace(" ", string.Empty).Replace("-", string.Empty);
                var normalizedManifestName = manifestName.Replace(" ", string.Empty).Replace("-", string.Empty);

                if (!normalizedManifestName.Contains(normalizedItemName, StringComparison.OrdinalIgnoreCase) &&
                    !normalizedItemName.Contains(normalizedManifestName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Publisher + content type + name all match - this is enough to consider it installed
            // Version matching is optional and used for additional confirmation
            var manifestVersion = manifest.Version ?? string.Empty;
            var manifestDatePart = ExtractDateFromVersion(manifestVersion);

            // Direct version match - strongest confirmation
            if (manifestVersion.Equals(itemVersion, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Date part match (e.g., "20251121" from "weekly-2025-11-21")
            if (!string.IsNullOrEmpty(itemDatePart) &&
                !string.IsNullOrEmpty(manifestDatePart) &&
                itemDatePart.Equals(manifestDatePart, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // If publisher + content type + name match but versions differ,
            // do not consider it installed - this could be a different version
            // and would hide available updates from the user
        }

        return false;
    }

    [RelayCommand]
    private async Task ToggleExpandAsync()
    {
        IsExpanded = !IsExpanded;
        _logger.LogDebug("Publisher card {PublisherId} expanded: {IsExpanded}", PublisherId, IsExpanded);

        if (IsExpanded)
        {
            await RefreshInstallationStatusAsync();
        }
    }

    [RelayCommand]
    private async Task DownloadContentAsync(ContentItemViewModel item)
    {
        if (item.IsDownloaded)
        {
            _logger.LogDebug("Content {ItemName} is already downloaded", item.Name);
            return;
        }

        if (item.Model == null)
        {
            _logger.LogError("Cannot install item {ItemName}: Model is null", item.Name);
            item.DownloadStatus = "Error: No source available";
            return;
        }

        try
        {
            _logger.LogInformation("Starting download of {ItemName} v{Version}", item.Name, item.Version);
            item.IsDownloading = true;
            item.DownloadStatus = "Downloading...";
            item.DownloadProgress = 0;

            var progress = new Progress<GenHub.Core.Models.Content.ContentAcquisitionProgress>(p =>
            {
                item.DownloadProgress = (int)p.ProgressPercentage;
                item.DownloadStatus = FormatProgressStatus(p);
            });

            var result = await _contentOrchestrator.AcquireContentAsync(item.Model, progress);

            if (result.Success)
            {
                item.DownloadStatus = "✓ Downloaded";
                item.DownloadProgress = 100;
                item.IsDownloaded = true;

                // Update the Model.Id with the resolved manifest ID
                if (result.Data != null)
                {
                    item.Model.Id = result.Data.Id.Value;
                    _logger.LogDebug("Updated Model.Id to resolved manifest ID: {ManifestId}", item.Model.Id);
                }

                _logger.LogInformation("Successfully downloaded {ItemName}", item.Name);
                _notificationService?.ShowSuccess("Download Complete", $"Downloaded {item.Name}");
            }
            else
            {
                var errorMsg = result.Errors != null ? string.Join(", ", result.Errors) : "Unknown error";
                item.DownloadStatus = $"✗ Failed: {errorMsg}";
                _logger.LogError("Failed to download {ItemName}: {Error}", item.Name, errorMsg);
            }
        }
        catch (Exception ex)
        {
            item.DownloadStatus = $"✗ Error: {ex.Message}";
            _logger.LogError(ex, "Exception during download of {ItemName}", item.Name);
        }
        finally
        {
            item.IsDownloading = false;
        }
    }

    [RelayCommand]
    private void ViewDetails()
    {
        _logger.LogDebug("Viewing details for {PublisherName}", DisplayName);

        // TODO: Open publisher details view or website
    }

    /// <summary>
    /// Installs the latest content item from this publisher.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task InstallLatestAsync()
    {
        var latestItem = ContentTypes
            .SelectMany(g => g.Items)
            .Where(i => !i.IsInstalled)
            .OrderByDescending(i => i.Model.LastUpdated)
            .FirstOrDefault();

        if (latestItem == null)
        {
            _logger.LogInformation("No uninstalled content available for {PublisherId}", PublisherId);
            return;
        }

        await DownloadContentAsync(latestItem);
    }

    [RelayCommand]
    private async Task AddToProfileAsync(object? args)
    {
        if (args is not object[] parameters || parameters.Length != 2)
        {
            return;
        }

        if (parameters[0] is not ContentItemViewModel item || parameters[1] is not GameProfile profile)
        {
            return;
        }

        if (_profileContentService == null)
        {
            _logger.LogWarning("Cannot add to profile: ProfileContentService is null");
            return;
        }

        if (item.IsDownloading)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Adding {ItemName} to profile {ProfileName}", item.Name, profile.Name);
            item.DownloadStatus = "Adding to profile...";

            var result = await _profileContentService.AddContentToProfileAsync(
                profile.Id,
                item.Model.Id,
                CancellationToken.None);

            if (result.Success)
            {
                item.DownloadStatus = $"✓ Added to {profile.Name}";

                if (result.WasContentSwapped)
                {
                    _notificationService?.ShowInfo(
                        "Content Replaced",
                        $"Replaced '{result.SwappedContentName}' with '{item.Name}' in profile '{profile.Name}'");

                    _logger.LogInformation(
                        "Content swap: replaced {OldContent} with {NewContent} in profile {ProfileName}",
                        result.SwappedContentName,
                        item.Name,
                        profile.Name);
                }
                else
                {
                    _notificationService?.ShowSuccess(
                        "Added to Profile",
                        $"'{item.Name}' added to profile '{profile.Name}'");
                }

                await RefreshAvailableProfilesAsync();
            }
            else
            {
                item.DownloadStatus = $"✗ Failed: {result.FirstError}";
                _notificationService?.ShowError(
                    "Failed to Add to Profile",
                    result.FirstError ?? "Unknown error occurred");
                _logger.LogError("Failed to add content to profile: {Error}", result.FirstError);
            }
        }
        catch (Exception ex)
        {
            item.DownloadStatus = $"✗ Error: {ex.Message}";
            _notificationService?.ShowError(
                "Error Adding to Profile",
                $"An unexpected error occurred: {ex.Message}");
            _logger.LogError(ex, "Exception adding content to profile");
        }
    }

    [RelayCommand]
    private async Task CreateProfileWithContentAsync(ContentItemViewModel item)
    {
        if (_profileContentService == null)
        {
            return;
        }

        if (item.IsDownloading)
        {
            return;
        }

        try
        {
            var baseName = $"{item.Name} Profile";
            var profileName = baseName;
            var counter = 1;

            // Ensure unique name
            while (AvailableProfiles.Any(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)))
            {
                profileName = $"{baseName} ({counter++})";
            }

            _logger.LogInformation("Creating new profile '{ProfileName}' with {ItemName}", profileName, item.Name);
            item.DownloadStatus = "Creating profile...";

            var result = await _profileContentService.CreateProfileWithContentAsync(
                profileName,
                item.Model.Id,
                CancellationToken.None);

            if (result.Success && result.Data != null)
            {
                item.DownloadStatus = $"✓ Profile created: {result.Data.Name}";

                // Notify other components that a profile was created
                try
                {
                    var message = new ProfileCreatedMessage(result.Data);
                    WeakReferenceMessenger.Default.Send(message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send ProfileCreatedMessage");
                }

                await RefreshAvailableProfilesAsync();
            }
            else
            {
                item.DownloadStatus = $"✗ Failed: {result.FirstError}";
                _notificationService?.ShowError(
                    "Failed to Create Profile",
                    result.FirstError ?? "Unknown error occurred");
                _logger.LogError("Failed to create profile: {Error}", result.FirstError);
            }
        }
        catch (Exception ex)
        {
            item.DownloadStatus = $"✗ Error: {ex.Message}";
            _notificationService?.ShowError(
                "Error Creating Profile",
                $"An unexpected error occurred: {ex.Message}");
            _logger.LogError(ex, "Exception creating profile with content");
        }
    }

    private void RefreshProfilesOnUiThread()
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await RefreshAvailableProfilesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh profiles dropdown");
            }
        });
    }
}
