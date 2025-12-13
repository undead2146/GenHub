using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.Notifications;
using GenHub.Core.Models.Enums;
using GenHub.Features.Content.Services.ContentDiscoverers;
using GenHub.Features.Content.Services.GeneralsOnline;
using GenHub.Features.Content.ViewModels;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Downloads.ViewModels;

/// <summary>
/// ViewModel for the Downloads tab.
/// </summary>
public partial class DownloadsViewModel(
    IServiceProvider serviceProvider,
    ILogger<DownloadsViewModel> logger,
    INotificationService notificationService,
    GitHubTopicsDiscoverer gitHubTopicsDiscoverer) : ViewModelBase
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly GitHubTopicsDiscoverer _gitHubTopicsDiscoverer = gitHubTopicsDiscoverer;
    [ObservableProperty]
    private string _title = "Downloads";

    [ObservableProperty]
    private string _description = "Manage your downloads and installations";

    [ObservableProperty]
    private bool _isInstallingGeneralsOnline;

    [ObservableProperty]
    private string _installationStatus = string.Empty;

    [ObservableProperty]
    private double _installationProgress;

    [ObservableProperty]
    private string _generalsOnlineVersion = "Loading...";

    [ObservableProperty]
    private string _weeklyReleaseVersion = "Loading...";

    [ObservableProperty]
    private string _communityPatchVersion = "Loading...";

    [ObservableProperty]
    private string _communityPatchStatus = string.Empty;

    [ObservableProperty]
    private double _communityPatchProgress;

    [ObservableProperty]
    private ObservableCollection<PublisherCardViewModel> _publisherCards = new();

    /// <summary>
    /// Performs asynchronous initialization for the Downloads tab.
    /// Fetches latest version information from all publishers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task InitializeAsync()
    {
        try
        {
            // Initialize publisher cards
            InitializePublisherCards();

            // Fetch version information from all publishers concurrently
            var generalsOnlineTask = FetchGeneralsOnlineVersionAsync();
            var weeklyReleaseTask = FetchWeeklyReleaseVersionAsync();
            var communityPatchTask = FetchCommunityPatchVersionAsync();

            await Task.WhenAll(generalsOnlineTask, weeklyReleaseTask, communityPatchTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Downloads view");
        }
    }

    /// <summary>
    /// Called when the tab is activated/navigated to.
    /// Refreshes installation status for all publisher cards.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task OnTabActivatedAsync()
    {
        try
        {
            foreach (var card in PublisherCards)
            {
                if (card.IsExpanded)
                {
                    await card.RefreshInstallationStatusAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh installation status on tab activation");
        }
    }

    private void InitializePublisherCards()
    {
        // Create Generals Online publisher card (Feature 2)
        var generalsOnlineCard = serviceProvider.GetService(typeof(PublisherCardViewModel)) as PublisherCardViewModel;
        if (generalsOnlineCard != null)
        {
            generalsOnlineCard.PublisherId = GeneralsOnlineConstants.PublisherType;
            generalsOnlineCard.DisplayName = GeneralsOnlineConstants.ContentName;
            generalsOnlineCard.IconColor = GeneralsOnlineConstants.IconColor;
            generalsOnlineCard.ReleaseNotes = GeneralsOnlineConstants.ShortDescription;
            generalsOnlineCard.IsLoading = true;
            PublisherCards.Add(generalsOnlineCard);
        }

        // Create TheSuperHackers publisher card
        var superHackersCard = serviceProvider.GetService(typeof(PublisherCardViewModel)) as PublisherCardViewModel;
        if (superHackersCard != null)
        {
            superHackersCard.PublisherId = PublisherTypeConstants.TheSuperHackers;
            superHackersCard.DisplayName = SuperHackersConstants.PublisherName;
            superHackersCard.IconColor = SuperHackersConstants.IconColor;
            superHackersCard.ReleaseNotes = SuperHackersConstants.ProviderDescription;
            superHackersCard.IsLoading = true;
            PublisherCards.Add(superHackersCard);
        }

        // Create Community Outpost publisher card
        var communityOutpostCard = serviceProvider.GetService(typeof(PublisherCardViewModel)) as PublisherCardViewModel;
        if (communityOutpostCard != null)
        {
            communityOutpostCard.PublisherId = CommunityOutpostConstants.PublisherType;
            communityOutpostCard.DisplayName = CommunityOutpostConstants.PublisherName;
            communityOutpostCard.IconColor = CommunityOutpostConstants.IconColor;
            communityOutpostCard.ReleaseNotes = CommunityOutpostConstants.ProviderDescription;
            communityOutpostCard.IsLoading = true;
            PublisherCards.Add(communityOutpostCard);
        }

        // Create GitHub publisher card (topic-based discovery)
        var githubCard = serviceProvider.GetService(typeof(PublisherCardViewModel)) as PublisherCardViewModel;
        if (githubCard != null)
        {
            githubCard.PublisherId = GitHubTopicsConstants.PublisherType;
            githubCard.DisplayName = GitHubTopicsConstants.PublisherName;
            githubCard.IconColor = GitHubTopicsConstants.IconColor;
            githubCard.ReleaseNotes = GitHubTopicsConstants.ProviderDescription;
            githubCard.IsLoading = true;
            PublisherCards.Add(githubCard);
        }

        // Populate cards with real content asynchronously
        _ = PopulatePublisherCardsAsync();
    }

    private async Task PopulatePublisherCardsAsync()
    {
        await Task.WhenAll(
            PopulateGeneralsOnlineCardAsync(),
            PopulateSuperHackersCardAsync(),
            PopulateCommunityOutpostCardAsync(),
            PopulateGithubCardAsync());
    }

    private async Task PopulateGeneralsOnlineCardAsync()
    {
        try
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == GeneralsOnlineConstants.PublisherType);
            if (card == null) return;

            var discoverer = serviceProvider.GetService(typeof(GeneralsOnlineDiscoverer)) as GeneralsOnlineDiscoverer;
            if (discoverer == null) return;

            var result = await discoverer.DiscoverAsync(new ContentSearchQuery());
            if (result.Success && result.Data?.Any() == true)
            {
                var releases = result.Data.ToList();

                // Group by content type
                var groupedContent = releases.GroupBy(r => r.ContentType).ToList();

                foreach (var group in groupedContent)
                {
                    var contentGroup = new ContentTypeGroup
                    {
                        Type = group.Key,
                        DisplayName = GetContentTypeDisplayName(group.Key),
                        Count = group.Count(),
                        Items = new ObservableCollection<ContentItemViewModel>(
                            group.Select(item => new ContentItemViewModel(item))),
                    };
                    card.ContentTypes.Add(contentGroup);
                }

                // Set card metadata from first release
                var latest = releases.FirstOrDefault();
                if (latest != null)
                {
                    card.LatestVersion = latest.Version;

                    // card.ReleaseNotes = latest.Description ?? card.ReleaseNotes; // Keep generic description
                    card.DownloadSize = latest.DownloadSize;
                    card.ReleaseDate = latest.LastUpdated;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to populate Generals Online card");
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == GeneralsOnlineConstants.PublisherType);
            if (card != null)
            {
                card.HasError = true;
                card.ErrorMessage = "Failed to load content";
            }
        }
        finally
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == GeneralsOnlineConstants.PublisherType);
            if (card != null) card.IsLoading = false;
        }
    }

    private async Task PopulateSuperHackersCardAsync()
    {
        try
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == PublisherTypeConstants.TheSuperHackers);
            if (card == null) return;

            // Query for all configured GitHub releases
            var query = new ContentSearchQuery();
            var gitHubDiscoverer = serviceProvider.GetService(typeof(GenHub.Features.Content.Services.ContentDiscoverers.GitHubReleasesDiscoverer)) as GenHub.Features.Content.Services.ContentDiscoverers.GitHubReleasesDiscoverer;
            if (gitHubDiscoverer == null)
            {
                logger.LogWarning("GitHubReleasesDiscoverer not available for SuperHackers card");
                return;
            }

            // Query for all configured GitHub releases
            var searchQuery = new ContentSearchQuery();

            var result = await gitHubDiscoverer.DiscoverAsync(searchQuery);
            if (result.Success && result.Data?.Any() == true)
            {
                // Filter for SuperHackers content if the discoverer returns more (though config should limit it)
                // And patch the ProviderName to ensure we use the SuperHackersProvider
                var releases = result.Data.Select(r =>
                {
                    r.ProviderName = GenHub.Core.Constants.PublisherTypeConstants.TheSuperHackers;
                    return r;
                }).ToList();

                // Group by content type (Patch, GameClient, Tools)
                var groupedContent = releases.GroupBy(r => r.ContentType).ToList();

                foreach (var group in groupedContent)
                {
                    var contentGroup = new ContentTypeGroup
                    {
                        Type = group.Key,
                        DisplayName = GetContentTypeDisplayName(group.Key),
                        Count = group.Count(),
                        Items = new ObservableCollection<ContentItemViewModel>(
                            group.Select(item => new ContentItemViewModel(item))),
                    };
                    card.ContentTypes.Add(contentGroup);
                }

                // Set card metadata from latest release
                var latest = releases.OrderByDescending(r => r.LastUpdated).FirstOrDefault();
                if (latest != null)
                {
                    card.LatestVersion = latest.Version;
                    card.DownloadSize = latest.DownloadSize;
                    card.ReleaseDate = latest.LastUpdated;
                }

                logger.LogInformation("Populated SuperHackers card with {Count} releases", releases.Count);
            }
            else
            {
                logger.LogWarning("No releases found for SuperHackers");
                card.LatestVersion = "No releases";
                card.ReleaseNotes = "Check GitHub for updates";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to populate SuperHackers card");
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == PublisherTypeConstants.TheSuperHackers);
            if (card != null)
            {
                card.HasError = true;
                card.ErrorMessage = "Failed to load GitHub releases";
            }
        }
        finally
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == PublisherTypeConstants.TheSuperHackers);
            if (card != null) card.IsLoading = false;
        }
    }

    private async Task PopulateCommunityOutpostCardAsync()
    {
        try
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == CommunityOutpostConstants.PublisherType);
            if (card == null) return;

            var discoverer = serviceProvider.GetService(typeof(GenHub.Features.Content.Services.CommunityOutpost.CommunityOutpostDiscoverer)) as GenHub.Features.Content.Services.CommunityOutpost.CommunityOutpostDiscoverer;
            if (discoverer == null) return;

            var result = await discoverer.DiscoverAsync(new ContentSearchQuery());
            if (result.Success && result.Data?.Any() == true)
            {
                var releases = result.Data.ToList();

                // Group by content type
                var groupedContent = releases.GroupBy(r => r.ContentType).ToList();

                foreach (var group in groupedContent)
                {
                    var contentGroup = new ContentTypeGroup
                    {
                        Type = group.Key,
                        DisplayName = GetContentTypeDisplayName(group.Key),
                        Count = group.Count(),
                        Items = new ObservableCollection<ContentItemViewModel>(
                            group.Select(item => new ContentItemViewModel(item))),
                    };
                    card.ContentTypes.Add(contentGroup);
                }

                // Set card metadata from first release
                var latest = releases.FirstOrDefault();
                if (latest != null)
                {
                    card.LatestVersion = latest.Version;
                    card.DownloadSize = latest.DownloadSize;
                    card.ReleaseDate = latest.LastUpdated;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to populate Community Outpost card");
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == CommunityOutpostConstants.PublisherType);
            if (card != null)
            {
                card.HasError = true;
                card.ErrorMessage = "Failed to load content from legi.cc";
            }
        }
        finally
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == CommunityOutpostConstants.PublisherType);
            if (card != null) card.IsLoading = false;
        }
    }

    private async Task PopulateGithubCardAsync()
    {
        var card = PublisherCards.FirstOrDefault(c => c.PublisherId == GitHubTopicsConstants.PublisherType);
        if (card == null) return;

        try
        {
            if (_gitHubTopicsDiscoverer == null)
            {
                logger.LogWarning("GitHubTopicsDiscoverer not available for GitHub card");
                card.HasError = true;
                card.ErrorMessage = "GitHub discoverer service not available";
                card.IsLoading = false;
                return;
            }

            var result = await _gitHubTopicsDiscoverer.DiscoverAsync(new ContentSearchQuery());
            if (result.Success && result.Data?.Any() == true)
            {
                var releases = result.Data.ToList();

                // Group by content type
                var groupedContent = releases.GroupBy(r => r.ContentType).ToList();

                foreach (var group in groupedContent)
                {
                    var contentGroup = new ContentTypeGroup
                    {
                        Type = group.Key,
                        DisplayName = GetContentTypeDisplayName(group.Key),
                        Count = group.Count(),
                        Items = new ObservableCollection<ContentItemViewModel>(
                            group.Select(item => new ContentItemViewModel(item))),
                    };
                    card.ContentTypes.Add(contentGroup);
                }

                // Set card metadata from most starred repository
                var latest = releases.OrderByDescending(r =>
                {
                    if (r.ResolverMetadata.TryGetValue(GitHubTopicsConstants.StarCountMetadataKey, out var stars) &&
                        int.TryParse(stars, out var starCount))
                    {
                        return starCount;
                    }

                    return 0;
                }).FirstOrDefault();

                if (latest != null)
                {
                    card.LatestVersion = $"{releases.Count} repos";
                    card.DownloadSize = releases.Sum(r => r.DownloadSize);
                    card.ReleaseDate = releases.Max(r => r.LastUpdated);
                }

                logger.LogInformation("Populated GitHub card with {Count} repositories", releases.Count);
                _notificationService.ShowSuccess(
                    "GitHub Discovery Complete",
                    $"Found {releases.Count} community repositories with {groupedContent.Count} content types.");
            }
            else
            {
                logger.LogInformation("No GitHub repositories found with GenHub topics");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to populate GitHub card");
            card.HasError = true;
            card.ErrorMessage = "Failed to discover GitHub repositories";
            card.IsLoading = false;

            _notificationService.ShowError(
                "GitHub Discovery Failed",
                $"Failed to discover GitHub repositories: {ex.Message}");
        }
        finally
        {
            card.IsLoading = false;
        }
    }

    private string GetContentTypeDisplayName(ContentType type)
    {
        return type switch
        {
            ContentType.GameClient => UiConstants.GameClientDisplayName,
            ContentType.MapPack => UiConstants.MapPackDisplayName,
            ContentType.Patch => UiConstants.PatchDisplayName,
            ContentType.Addon => UiConstants.AddonDisplayName,
            ContentType.Mod => UiConstants.ModDisplayName,
            ContentType.Mission => UiConstants.MissionDisplayName,
            ContentType.Map => UiConstants.MapDisplayName,
            ContentType.LanguagePack => UiConstants.LanguagePackDisplayName,
            ContentType.ContentBundle => UiConstants.ContentBundleDisplayName,
            _ => type.ToString()
        };
    }

    private async Task FetchGeneralsOnlineVersionAsync()
    {
        try
        {
            var discoverer = serviceProvider.GetService(typeof(GeneralsOnlineDiscoverer)) as GeneralsOnlineDiscoverer;
            if (discoverer != null)
            {
                var result = await discoverer.DiscoverAsync(new ContentSearchQuery());
                if (result.Success && result.Data?.Any() == true)
                {
                    var firstResult = result.Data.First();
                    GeneralsOnlineVersion = $"v{firstResult.Version}";
                    logger.LogInformation("Fetched GeneralsOnline version: {Version}", firstResult.Version);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch GeneralsOnline version");
            GeneralsOnlineVersion = "Unavailable";
        }
    }

    private async Task FetchWeeklyReleaseVersionAsync()
    {
        try
        {
            var discoverer = serviceProvider.GetService(typeof(GenHub.Features.Content.Services.ContentDiscoverers.GitHubReleasesDiscoverer)) as GenHub.Features.Content.Services.ContentDiscoverers.GitHubReleasesDiscoverer;
            if (discoverer != null)
            {
                var result = await discoverer.DiscoverAsync(new ContentSearchQuery());
                if (result.Success && result.Data?.Any() == true)
                {
                    // Filter for SuperHackers content if needed, similar to PopulateSuperHackersCardAsync
                    // For now, assuming the discoverer returns relevant releases based on config
                    var latest = result.Data.OrderByDescending(r => r.LastUpdated).FirstOrDefault();
                    if (latest != null)
                    {
                        WeeklyReleaseVersion = latest.Version;
                        logger.LogInformation("Fetched Weekly Release version: {Version}", latest.Version);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch weekly release version");
            WeeklyReleaseVersion = "Unavailable";
        }
    }

    private async Task FetchCommunityPatchVersionAsync()
    {
        try
        {
            var discoverer = serviceProvider.GetService(typeof(GenHub.Features.Content.Services.CommunityOutpost.CommunityOutpostDiscoverer)) as GenHub.Features.Content.Services.CommunityOutpost.CommunityOutpostDiscoverer;
            if (discoverer != null)
            {
                var result = await discoverer.DiscoverAsync(new ContentSearchQuery());
                if (result.Success && result.Data?.Any() == true)
                {
                    var firstResult = result.Data.First();
                    CommunityPatchVersion = firstResult.Version;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch community patch version");
            CommunityPatchVersion = "Unavailable";
        }
    }

    [RelayCommand]
    private async Task InstallGeneralsOnlineAsync()
    {
        IsInstallingGeneralsOnline = true;
        InstallationStatus = "Starting...";
        InstallationProgress = 0;

        try
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == GeneralsOnlineConstants.PublisherType);
            if (card != null)
            {
                await card.InstallLatestCommand.ExecuteAsync(null);

                // Mirror the card's status if possible, or just reset after a delay since we can't easily bind to the card's internal progress from here without more complex binding
                // For now, we'll assume the card handles the actual installation UI feedback
                InstallationStatus = "Installation started via card";
            }
            else
            {
                logger.LogWarning("Generals Online card not found for installation");
                InstallationStatus = "Card not found";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Generals Online installation");
            InstallationStatus = "Error";
        }
        finally
        {
            await Task.Delay(2000); // Show status for a bit
            IsInstallingGeneralsOnline = false;
            InstallationStatus = string.Empty;
        }
    }

    [RelayCommand]
    private async Task GetWeeklyReleaseAsync()
    {
        try
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == PublisherTypeConstants.TheSuperHackers);
            if (card != null)
            {
                await card.InstallLatestCommand.ExecuteAsync(null);
            }
            else
            {
                logger.LogWarning("SuperHackers card not found for installation");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Weekly Release installation");
        }
    }

    [RelayCommand]
    private async Task GetCommunityPatchAsync()
    {
        try
        {
            var card = PublisherCards.FirstOrDefault(c => c.PublisherId == CommunityOutpostConstants.PublisherType);
            if (card != null)
            {
                await card.InstallLatestCommand.ExecuteAsync(null);
            }
            else
            {
                logger.LogWarning("Community Outpost card not found for installation");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Community Patch installation");
        }
    }
}