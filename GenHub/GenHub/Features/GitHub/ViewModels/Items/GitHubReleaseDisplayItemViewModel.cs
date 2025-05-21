using System;
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
    /// Specialized view model for GitHub release items
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
            Name = release.Name ?? string.Empty;
            TagName = release.TagName;
            Body = release.Body ?? string.Empty;
            IsDraft = release.Draft; // Changed from IsDraft to Draft
            IsPrerelease = release.Prerelease;
            AssetCount = release.Assets?.Count ?? 0;
            
            // Set icon based on release state
            UpdateIconKey();
        }
        
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
        /// Gets whether this is a release
        /// </summary>
        public override bool IsRelease => true;
        
        /// <summary>
        /// Gets whether this release can be expanded
        /// </summary>
        public override bool IsExpandable => _release.Assets?.Any() ?? false;
        
        /// <summary>
        /// Gets the release version
        /// </summary>
        public string Version => _release.Version ?? string.Empty;
        
        /// <summary>
        /// Gets the HTML URL for the release
        /// </summary>
        public string HtmlUrl => _release.HtmlUrl ?? string.Empty;
        
        // Direct release property accessors with renamed property names to avoid ambiguity
        public long ReleaseId => _release.Id;
        public string ReleaseName => _release.Name ?? string.Empty;
        public string ReleaseTagName => _release.TagName;
        public bool IsReleaseDraft => _release.Draft;  // Access Draft, not IsDraft
        public bool IsReleasePrerelease => _release.Prerelease;
        public DateTime PublishedAt => _release.PublishedAt;
        public string? ReleaseBody => _release.Body;
        
        // Keep Id property as is since it doesn't conflict
        public long Id => _release.Id;
        public bool HasAssets => Assets.Count > 0;

        /// <summary>
        /// Loads the child items for this release (its assets)
        /// </summary>
        public override async Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            if (_assetsLoaded || IsLoadingAssets)
                return;
                
            IsLoadingAssets = true;
            
            try
            {
                _logger.LogDebug("Loading assets for release {ReleaseName} ({ReleaseId})", 
                    ReleaseName, Id);
                
                if (_release.Assets == null || !_release.Assets.Any())
                {
                    _logger.LogInformation("No assets found for release {ReleaseId}", Id);
                    _assetsLoaded = true;
                    return;
                }
                
                // Convert the assets to view models
                var assetViewModels = _release.Assets.Select(a => 
                    new GitHubReleaseAssetViewModel(a, this, _gitHubService, _logger)).ToList();
                
                // Add to the observable collection on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
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
                });
                
                _assetsLoaded = true;
                SetLoadedState(true);
                await LoadChildrenInternalAsync(cancellationToken);
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
        /// Additional internal child loading logic
        /// </summary>
        protected override async Task LoadChildrenInternalAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Creating view models for release assets: {ReleaseName}", ReleaseName);
                
                // No need to recreate asset view models if they're already created from LoadChildrenAsync
                // This method serves as an extension point for derived classes
                
                await Task.CompletedTask; // For future async operations
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading release assets for {ReleaseName}", ReleaseName);
            }
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
