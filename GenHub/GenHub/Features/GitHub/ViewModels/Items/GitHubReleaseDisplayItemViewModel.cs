using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces;
using Avalonia.Threading;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for GitHub release items with asset loading capabilities
    /// </summary>
    public partial class GitHubReleaseDisplayItemViewModel : GitHubDisplayItemViewModel
    {
        private readonly GitHubRelease _release;
        private readonly IGitHubDisplayItemFactory _displayItemFactory;
        private readonly IGitHubServiceFacade _gitHubService;
        private readonly ILogger _logger;
        
        private bool _assetsLoaded;
        
        [ObservableProperty]
        private ObservableCollection<IGitHubDisplayItem> _assets = new();
        
        [ObservableProperty]
        private bool _isLoadingAssets;

        [ObservableProperty]
        private string _name = string.Empty;
        
        [ObservableProperty]
        private string _tagName = string.Empty;
        
        [ObservableProperty]
        private string _body = string.Empty;
        
        [ObservableProperty]
        private bool _isDraft;
        
        [ObservableProperty]
        private bool _isPrerelease;
        
        [ObservableProperty]
        private int _assetCount;
        
        /// <summary>
        /// Initializes a new instance of the GitHubReleaseDisplayItemViewModel class
        /// </summary>
        /// <param name="release">The GitHub release data</param>
        /// <param name="displayItemFactory">Factory for creating display items</param>
        /// <param name="gitHubService">The GitHub service facade</param>
        /// <param name="logger">Logger for diagnostics</param>
        public GitHubReleaseDisplayItemViewModel(
            GitHubRelease release,
            IGitHubDisplayItemFactory displayItemFactory,
            IGitHubServiceFacade gitHubService,
            ILogger logger)
        {
            _release = release ?? throw new ArgumentNullException(nameof(release));
            _displayItemFactory = displayItemFactory ?? throw new ArgumentNullException(nameof(displayItemFactory));
            _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize properties from release model
            InitializeProperties();
            
            // Set icon based on release state
            UpdateIconKey();
            
            _logger.LogDebug("Created GitHubReleaseDisplayItemViewModel for release {ReleaseId}: {ReleaseName}", 
                Id, ReleaseName);
        }

        /// <summary>
        /// Initializes observable properties from the release data
        /// </summary>
        private void InitializeProperties()
        {
            Name = _release.Name ?? string.Empty;
            TagName = _release.TagName;
            Body = _release.Body ?? string.Empty;
            IsDraft = _release.Draft;
            IsPrerelease = _release.Prerelease;
            AssetCount = _release.Assets?.Count ?? 0;
        }

        #region Overridden Properties
        /// <summary>
        /// Gets the display name for the release
        /// </summary>
        public override string DisplayName => $"{Name} ({TagName})";
        
        /// <summary>
        /// Gets the date for sorting
        /// </summary>
        public override DateTime SortDate => _release.PublishedAt;

        /// <summary>
        /// Gets the description for the release
        /// </summary>
        public override string Description => _release.Body ?? string.Empty;
        
        /// <summary>
        /// Gets a value indicating whether this is a release
        /// </summary>
        public override bool IsRelease => true;
        
        /// <summary>
        /// Gets a value indicating whether this release can be expanded
        /// </summary>
        public override bool IsExpandable => _release.Assets?.Any() ?? false;
        #endregion

        #region Release Properties
        /// <summary>
        /// Gets the release version
        /// </summary>
        public string Version => _release.Version ?? string.Empty;
        
        /// <summary>
        /// Gets the HTML URL for the release
        /// </summary>
        public string HtmlUrl => _release.HtmlUrl ?? string.Empty;
        
        /// <summary>
        /// Gets the release ID
        /// </summary>
        public long Id => _release.Id;
        
        /// <summary>
        /// Gets the release name
        /// </summary>
        public string ReleaseName => _release.Name ?? string.Empty;
        
        /// <summary>
        /// Gets the release tag name
        /// </summary>
        public string ReleaseTagName => _release.TagName;
        
        /// <summary>
        /// Gets a value indicating whether this is a draft release
        /// </summary>
        public bool IsReleaseDraft => _release.Draft;
        
        /// <summary>
        /// Gets a value indicating whether this is a prerelease
        /// </summary>
        public bool IsReleasePrerelease => _release.Prerelease;
        
        /// <summary>
        /// Gets the publish date
        /// </summary>
        public DateTime PublishedAt => _release.PublishedAt;
        
        /// <summary>
        /// Gets the release body/description
        /// </summary>
        public string? ReleaseBody => _release.Body;
        
        /// <summary>
        /// Gets a value indicating whether this release has assets
        /// </summary>
        public bool HasAssets => Assets.Count > 0;
        #endregion

        /// <summary>
        /// Loads the child items for this release (its assets)
        /// </summary>
        public override async Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            if (_assetsLoaded || IsLoadingAssets)
            {
                _logger.LogDebug("Skipping LoadChildrenAsync - already loaded or loading for release {ReleaseId}", Id);
                return;
            }
                
            IsLoadingAssets = true;
            
            try
            {
                _logger.LogDebug("Loading assets for release {ReleaseName} ({ReleaseId})", ReleaseName, Id);
                
                if (_release.Assets == null || !_release.Assets.Any())
                {
                    _logger.LogInformation("No assets found for release {ReleaseId}", Id);
                    _assetsLoaded = true;
                    return;
                }
                
                // Convert the assets to view models
                var assetViewModels = _release.Assets
                    .Select(CreateAssetViewModel)
                    .Where(vm => vm != null)
                    .ToList();
                
                _logger.LogDebug("Created {Count} asset view models for release {ReleaseId}", assetViewModels.Count, Id);
                
                // Add to the observable collection on UI thread
                await UpdateUIWithAssets(assetViewModels);
                
                _assetsLoaded = true;
                SetLoadedState(true);
                
                _logger.LogInformation("Successfully loaded {Count} assets for release {ReleaseId}", assetViewModels.Count, Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assets for release {ReleaseId}", Id);
            }
            finally
            {
                IsLoadingAssets = false;
            }
        }

        /// <summary>
        /// Creates an asset view model with proper error handling
        /// </summary>
        private GitHubReleaseAssetViewModel? CreateAssetViewModel(GitHubReleaseAsset asset)
        {
            try
            {
                return new GitHubReleaseAssetViewModel(asset, this, _gitHubService, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating asset view model for {AssetName}", asset?.Name);
                return null;
            }
        }

        /// <summary>
        /// Updates the UI with asset view models
        /// </summary>
        private async Task UpdateUIWithAssets(List<GitHubReleaseAssetViewModel> assetViewModels)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    Assets.Clear();
                    foreach (var asset in assetViewModels)
                    {
                        Assets.Add(asset);
                    }
                    
                    // Also add to Children collection for IGitHubDisplayItem interface
                    Children.Clear();
                    foreach (var asset in assetViewModels)
                    {
                        Children.Add(asset);
                    }
                    
                    OnPropertyChanged(nameof(HasAssets));
                    
                    _logger.LogDebug("UI updated with {Count} assets for release {ReleaseId}", assetViewModels.Count, Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating UI with assets for release {ReleaseId}", Id);
                }
            });
        }
        
        /// <summary>
        /// Updates the icon key based on release state
        /// </summary>
        private void UpdateIconKey()
        {
            if (IsDraft)
            {
                _iconKey = "ReleaseDraft";
            }
            else if (IsPrerelease)
            {
                _iconKey = "ReleasePrerelease";
            }
            else
            {
                _iconKey = "ReleaseStable";
            }
        }
    }
}
