using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Common;
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Interfaces.Manifest;
using GenHub.Core.Interfaces.Parsers;
using GenHub.Core.Models.Common;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Features.Content.Services.GeneralsOnline;
using GenHub.Features.Downloads.ViewModels.Filters;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Downloads.ViewModels;

/// <summary>
/// ViewModel for the redesigned Downloads browser with sidebar navigation and content grid.
/// </summary>
public partial class DownloadsBrowserViewModel(
    IServiceProvider serviceProvider,
    ILogger<DownloadsBrowserViewModel> logger,
    IEnumerable<IContentDiscoverer> contentDiscoverers,
    IDownloadService downloadService) : ObservableObject
{
    private readonly Dictionary<string, IFilterPanelViewModel> _filterViewModels = [];

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private bool _isFilterPanelVisible;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<PublisherItemViewModel> _publishers = [];

    [ObservableProperty]
    private PublisherItemViewModel? _selectedPublisher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowFilters))]
    private IFilterPanelViewModel? _currentFilterViewModel;

    /// <summary>
    /// Gets a value indicating whether filters are available for the current publisher.
    /// </summary>
    public bool CanShowFilters => CurrentFilterViewModel != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailViewVisible))]
    private ContentDetailViewModel? _selectedContent;

    [ObservableProperty]
    private ObservableCollection<ContentGridItemViewModel> _contentItems = [];

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private bool _canLoadMore;

    private CancellationTokenSource? _lastSearchCts;

    /// <summary>
    /// Gets a value indicating whether the detail view is currently visible.
    /// </summary>
    public bool IsDetailViewVisible => SelectedContent != null;

    [ObservableProperty]
    private int _pageSize = 24;

    /// <summary>
    /// Performs asynchronous initialization.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task InitializeAsync()
    {
        InitializePublishers();
        InitializeFilterViewModels();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the Downloads tab is activated.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnTabActivatedAsync()
    {
        if (ContentItems.Count == 0 && !IsLoading)
        {
            await RefreshContentAsync();
        }
    }

    [RelayCommand]
    private static void GoBack()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Core.Messages.ClosePublisherDetailsMessage());
    }

    partial void OnSelectedPublisherChanged(PublisherItemViewModel? value)
    {
        // Update selection state
        foreach (var publisher in Publishers)
        {
            publisher.IsSelected = publisher == value;
        }

        // Clear previous filter state
        if (CurrentFilterViewModel != null)
        {
            CurrentFilterViewModel.FiltersApplied -= OnFiltersApplied;
            CurrentFilterViewModel.FiltersCleared -= OnFiltersCleared;
            CurrentFilterViewModel.ClearFilters();
        }

        // Switch filter panel
        if (value != null && _filterViewModels.TryGetValue(value.PublisherId, out var filterVm))
        {
            CurrentFilterViewModel = filterVm;
            CurrentFilterViewModel.FiltersApplied += OnFiltersApplied;
            CurrentFilterViewModel.FiltersCleared += OnFiltersCleared;
        }
        else
        {
            CurrentFilterViewModel = null;
        }

        // Trigger content refresh
        CurrentPage = 1;
        CanLoadMore = false;

        // Close detail view
        SelectedContent = null;

        _ = RefreshContentAsync();
    }

    private void OnFiltersCleared(object? sender, EventArgs e)
    {
        // Trigger content refresh when filters are cleared
        _ = RefreshContentAsync();
    }

    private void OnFiltersApplied(object? sender, EventArgs e)
    {
        // Trigger content refresh when filters are applied
        CurrentPage = 1;
        _ = RefreshContentAsync();
    }

    [RelayCommand]
    private void SelectPublisher(PublisherItemViewModel publisher)
    {
        SelectedPublisher = publisher;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await RefreshContentAsync();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (CanLoadMore && !IsLoading)
        {
            CurrentPage++;
            logger.LogInformation(
                "Loading more content for {Publisher}, page {Page}",
                SelectedPublisher?.PublisherId ?? "Unknown",
                CurrentPage);
            await RefreshContentAsync(append: true);
        }
    }

    /// <param name="append">Whether to append results to the current list instead of clearing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RefreshContentAsync(bool append = false)
    {
        if (SelectedPublisher == null)
        {
            return;
        }

        // Cancel previous search if still running
        _lastSearchCts?.Cancel();
        _lastSearchCts = new CancellationTokenSource();
        var ct = _lastSearchCts.Token;

        try
        {
            IsLoading = true;
            if (!append)
            {
                ContentItems.Clear();
            }

            // Build base query
            var baseQuery = new ContentSearchQuery
            {
                SearchTerm = SearchTerm,
                Take = PageSize,
                Page = CurrentPage,
                TargetGame = GameType.ZeroHour, // Global default
            };

            // Apply active filters from filter panel
            if (CurrentFilterViewModel != null)
            {
                baseQuery = CurrentFilterViewModel.ApplyFilters(baseQuery);
            }

            if (SelectedPublisher.PublisherId == PublisherTypeConstants.All)
            {
                await RefreshAllPublishersAsync(baseQuery, ct);
            }
            else
            {
                await RefreshSinglePublisherAsync(SelectedPublisher.PublisherId, baseQuery, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Search for {Publisher} was canceled", SelectedPublisher.PublisherId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh content for publisher {Publisher}", SelectedPublisher.PublisherId);
        }
        finally
        {
            // Only stop "main" loading if we aren't canceled
            if (!ct.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private async Task RefreshSinglePublisherAsync(string publisherId, ContentSearchQuery query, CancellationToken ct)
    {
        var discoverer = GetDiscovererForPublisher(publisherId);
        if (discoverer == null)
        {
            logger.LogWarning("No discoverer found for publisher {Publisher}", publisherId);
            return;
        }

        var result = await discoverer.DiscoverAsync(query, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (result.Success && result.Data != null)
        {
            var items = result.Data.Items.ToList();

            // Track existing IDs to prevent duplicates
            var existingIds = new HashSet<string>(ContentItems.Select(x => x.SearchResult.Id ?? string.Empty));

            var addedCount = 0;
            foreach (var item in items)
            {
                var itemId = item.Id ?? string.Empty;
                if (!existingIds.Contains(itemId))
                {
                    var vm = new ContentGridItemViewModel(item)
                    {
                        ViewCommand = ViewContentCommand,
                        DownloadCommand = DownloadContentCommand,
                    };
                    ContentItems.Add(vm);
                    existingIds.Add(itemId);
                    addedCount++;
                }
            }

            logger.LogInformation(
                "Added {AddedCount} new items out of {TotalCount} fetched for {Publisher} (page {Page}). HasMoreItems: {HasMore}",
                addedCount,
                items.Count,
                publisherId,
                query.Page,
                result.Data.HasMoreItems);

            // Update CanLoadMore based on the discoverer's explicit signal
            CanLoadMore = result.Data.HasMoreItems;
        }
        else
        {
            CanLoadMore = false;
            logger.LogWarning("Discovery failed or returned no data for {Publisher}. Success: {Success}", publisherId, result.Success);
        }
    }

    private async Task RefreshAllPublishersAsync(ContentSearchQuery query, CancellationToken ct)
    {
        // In "All" mode, we search multiple sources in parallel
        // We'll track if ANY provider has more items
        var anyProviderHasMore = false;
        var lockObj = new object();

        var tasks = contentDiscoverers
            .Where(d => d.IsEnabled && d.SourceName != PublisherTypeConstants.All)
            .Select(async d =>
            {
                try
                {
                    var result = await d.DiscoverAsync(query, ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    if (result.Success && result.Data != null)
                    {
                        var vmItems = result.Data.Items.Select(item =>
                        {
                            return new ContentGridItemViewModel(item)
                            {
                                ViewCommand = ViewContentCommand,
                                DownloadCommand = DownloadContentCommand,
                            };
                        }).ToList();

                        if (result.Data.HasMoreItems)
                        {
                            lock (lockObj)
                            {
                                anyProviderHasMore = true;
                            }
                        }

                        // Add to collection on main thread
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            foreach (var vm in vmItems)
                            {
                                ContentItems.Add(vm);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Discoverer {Source} failed in 'All' mode", d.SourceName);
                }
            });

        await Task.WhenAll(tasks);

        // Enable Load More if at least one provider has more content
        CanLoadMore = !ct.IsCancellationRequested && anyProviderHasMore;
    }

    /// <returns>The discoverer for the specified publisher, or null if not found.</returns>
    private IContentDiscoverer? GetDiscovererForPublisher(string publisherId)
    {
        return publisherId switch
        {
            PublisherTypeConstants.GeneralsOnline => contentDiscoverers.OfType<GeneralsOnlineDiscoverer>().FirstOrDefault(),
            PublisherTypeConstants.TheSuperHackers => contentDiscoverers.OfType<GenHub.Features.Content.Services.GitHub.GitHubReleasesDiscoverer>().FirstOrDefault(),
            CommunityOutpostConstants.PublisherType => contentDiscoverers.OfType<GenHub.Features.Content.Services.CommunityOutpost.CommunityOutpostDiscoverer>().FirstOrDefault(),
            ModDBConstants.PublisherType => contentDiscoverers.OfType<GenHub.Features.Content.Services.ContentDiscoverers.ModDBDiscoverer>().FirstOrDefault(),
            CNCLabsConstants.PublisherType => contentDiscoverers.OfType<GenHub.Features.Content.Services.ContentDiscoverers.CNCLabsMapDiscoverer>().FirstOrDefault(),
            GitHubTopicsConstants.PublisherType => contentDiscoverers.OfType<GenHub.Features.Content.Services.ContentDiscoverers.GitHubTopicsDiscoverer>().FirstOrDefault(),
            AODMapsConstants.PublisherType => contentDiscoverers.OfType<GenHub.Features.Content.Services.ContentDiscoverers.AODMapsDiscoverer>().FirstOrDefault(),
            _ => null,
        };
    }

    [RelayCommand]
    private void ViewContent(ContentGridItemViewModel item)
    {
        if (item?.SearchResult != null)
        {
            var contentLogger = serviceProvider.GetService(typeof(ILogger<ContentDetailViewModel>)) as ILogger<ContentDetailViewModel>;
            if (contentLogger is null)
            {
                logger.LogWarning("Could not resolve ILogger<ContentDetailViewModel> from service provider");
            }

            var parsers = serviceProvider.GetService(typeof(IEnumerable<IWebPageParser>)) as IEnumerable<IWebPageParser> ?? [];

            SelectedContent = new ContentDetailViewModel(item.SearchResult, serviceProvider, parsers, downloadService, contentLogger!, CloseDetail);
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedContent = null;
    }

    private void InitializePublishers()
    {
        Publishers =
        [
            new PublisherItemViewModel(
                PublisherTypeConstants.All,
                "All Publishers",
                "avares://GenHub/Assets/Logos/generalsonline-logo.png", // Use a generic logo for now
                "merged"),
            new PublisherItemViewModel(
                PublisherTypeConstants.GeneralsOnline,
                "Generals Online",
                "avares://GenHub/Assets/Logos/generalsonline-logo.png",
                "static"),
            new PublisherItemViewModel(
                PublisherTypeConstants.TheSuperHackers,
                "TheSuperHackers",
                "avares://GenHub/Assets/Logos/thesuperhackers-logo.png",
                "static"),
            new PublisherItemViewModel(
                CommunityOutpostConstants.PublisherType,
                "CommunityOutpost",
                "avares://GenHub/Assets/Logos/communityoutpost-logo.png",
                "static"),
            new PublisherItemViewModel(
                ModDBConstants.PublisherType,
                "ModDB",
                "avares://GenHub/Assets/Logos/moddb-logo.png",
                "dynamic"),
            new PublisherItemViewModel(
                CNCLabsConstants.PublisherType,
                "CNC Labs",
                "avares://GenHub/Assets/Logos/cnclabs-logo.png",
                "dynamic"),
            new PublisherItemViewModel(
                GitHubTopicsConstants.PublisherType,
                "GitHub",
                "avares://GenHub/Assets/Logos/github-logo.png",
                "dynamic"),
            new PublisherItemViewModel(
                AODMapsConstants.PublisherType,
                "AOD Maps",
                "avares://GenHub/Assets/Logos/aodmaps-logo.png",
                "dynamic"),
        ];

        // Select first publisher by default
        if (Publishers.Count > 0)
        {
            SelectedPublisher = Publishers[0];
        }
    }

    private void InitializeFilterViewModels()
    {
        // Static publisher filters
        _filterViewModels[PublisherTypeConstants.GeneralsOnline] =
            new StaticPublisherFilterViewModel(PublisherTypeConstants.GeneralsOnline);

        // Using the updated CommunityOutpost filter
        _filterViewModels[CommunityOutpostConstants.PublisherType] =
            new CommunityOutpostFilterViewModel();

        // Dynamic publisher filters
        _filterViewModels[ModDBConstants.PublisherType] = new ModDBFilterViewModel();
        _filterViewModels[CNCLabsConstants.PublisherType] = new CNCLabsFilterViewModel();
        _filterViewModels[GitHubTopicsConstants.PublisherType] = new GitHubFilterViewModel();
        _filterViewModels[AODMapsConstants.PublisherType] = new AODMapsFilterViewModel();
    }

    [RelayCommand]
    private async Task DownloadContentAsync(ContentGridItemViewModel item)
    {
        if (item == null || item.IsDownloading)
        {
            return;
        }

        CancellationToken cancellationToken = default; // We might want to support cancellation later

        try
        {
            item.IsDownloading = true;
            item.DownloadProgress = 0;
            item.DownloadStatus = "Resolving content...";

            logger.LogInformation("Starting download for content: {Name} ({Provider})", item.Name, item.ProviderName);

            // Get the content resolver for this provider
            if (serviceProvider.GetService(typeof(IEnumerable<IContentResolver>)) is not IEnumerable<IContentResolver> resolvers)
            {
                logger.LogError("No resolvers found in service provider");
                item.DownloadStatus = "Error: Internal service error";
                return;
            }

            var resolverId = !string.IsNullOrEmpty(item.SearchResult.ResolverId)
                ? item.SearchResult.ResolverId
                : item.ProviderName;

            var resolver = resolvers.FirstOrDefault(r => r.ResolverId.Equals(resolverId, StringComparison.OrdinalIgnoreCase));

            if (resolver == null)
            {
                logger.LogError("No resolver found for provider: {Provider} (ResolverId: {ResolverId})", item.ProviderName, resolverId);
                item.DownloadStatus = $"Error: No resolver found for {resolverId}";
                return;
            }

            // Resolve the content to get a manifest
            item.DownloadStatus = "Creating manifest...";
            var manifestResult = await resolver.ResolveAsync(item.SearchResult, cancellationToken);

            if (!manifestResult.Success || manifestResult.Data == null)
            {
                logger.LogError("Failed to resolve content: {Error}", manifestResult.FirstError);
                item.DownloadStatus = $"Error: {manifestResult.FirstError}";
                return;
            }

            var manifest = manifestResult.Data;
            logger.LogInformation("Manifest created: {ManifestId}", manifest.Id.Value);

            // 2. Prepare Temp Directory
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GenHub", "Downloads", manifest.Id.Value);
            System.IO.Directory.CreateDirectory(tempDir);

            // 3. Download Files
            item.DownloadStatus = "Downloading files...";
            var remoteFiles = manifest.Files.Where(f => f.SourceType == ContentSourceType.RemoteDownload).ToList();

            if (manifest.Files.Count == 0)
            {
                logger.LogWarning("Manifest contains no files");
                item.DownloadStatus = "Error: Manifest has no files";
                return;
            }

            if (remoteFiles.Count == 0)
            {
                logger.LogInformation("No remote files to download in manifest (content might be pre-downloaded or in CAS)");
            }

            foreach (var file in remoteFiles)
            {
                if (string.IsNullOrEmpty(file.SourcePath)) continue;

                // Use RelativePath if available, otherwise extract from SourcePath
                var fileName = !string.IsNullOrEmpty(file.RelativePath)
                    ? System.IO.Path.GetFileName(file.RelativePath)
                    : System.IO.Path.GetFileName(file.SourcePath);
                var targetPath = System.IO.Path.Combine(tempDir, fileName);

                item.DownloadStatus = $"Downloading {fileName}...";

                var downloadResult = await downloadService.DownloadFileAsync(
                        new Uri(file.SourcePath),
                        targetPath,
                        null,
                        new Progress<DownloadProgress>(p =>
                        {
                            // Map 0-100 progress
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => item.DownloadProgress = (int)p.Percentage);
                        }),
                        cancellationToken);

                if (!downloadResult.Success)
                {
                    logger.LogError("Failed to download file {Url}: {Error}", file.SourcePath, downloadResult.FirstError);
                    item.DownloadStatus = $"Error downloading {fileName}";
                    return;
                }
            }

            // 4. Store Manifest
            // Store the manifest in the pool
            item.DownloadStatus = "Storing manifest...";
            if (serviceProvider.GetService(typeof(IContentManifestPool)) is not IContentManifestPool manifestPool)
            {
                logger.LogError("IContentManifestPool service not available");
                item.DownloadStatus = "Error: Manifest storage service not available";
                return;
            }

            // Pass the temp directory as the source directory
            var addResult = await manifestPool.AddManifestAsync(manifest, tempDir, null, cancellationToken);

            if (!addResult.Success)
            {
                logger.LogError("Failed to store manifest: {Error}", addResult.FirstError);
                item.DownloadStatus = $"Error message: {addResult.FirstError}"; // "Error: ..." prefix handled by label usually, but keeping concise
                return;
            }

            // 5. Cleanup
            try
            {
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cleanup temp download directory {Dir}", tempDir);
            }

            item.DownloadProgress = 100;
            item.DownloadStatus = "Download complete!";
            item.IsDownloaded = true;

            logger.LogInformation("Successfully downloaded and stored content: {ManifestId}", manifest.Id.Value);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Download cancelled for: {Name}", item.Name);
            item.DownloadStatus = "Download cancelled";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading content: {Name}", item.Name);
            item.DownloadStatus = $"Error: {ex.Message}";
        }
        finally
        {
            item.IsDownloading = false;
        }
    }
}
