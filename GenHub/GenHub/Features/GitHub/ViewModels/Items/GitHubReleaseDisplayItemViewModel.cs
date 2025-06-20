using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

            InitializeProperties();
            UpdateIconKey();

            _logger.LogDebug("Created GitHubReleaseDisplayItemViewModel for release {ReleaseName} (ID: {ReleaseId})",
                release.Name, release.Id);
        }

        /// <summary>
        /// Initializes observable properties from the release data
        /// </summary>
        private void InitializeProperties()
        {
            Name = _release.Name ?? string.Empty;
            TagName = _release.TagName ?? string.Empty;
            Body = _release.Body ?? string.Empty;
            IsDraft = _release.Draft;
            IsPrerelease = _release.Prerelease;
            AssetCount = _release.Assets?.Count ?? 0;
        }

        #region Release Properties
        /// <summary>
        /// Gets the release version
        /// </summary>
        public string Version => _release.Version ?? string.Empty;
        
        /// <summary>
        /// Gets the HTML URL for the release
        /// </summary>
        public string HtmlUrl => _release.HtmlUrl ?? string.Empty;
        public long Id => _release.Id;
        public string ReleaseName => _release.Name ?? string.Empty;
        public string ReleaseTagName => _release.TagName;
        public bool IsReleaseDraft => _release.Draft;
        public bool IsReleasePrerelease => _release.Prerelease;
        public DateTime PublishedAt => _release.PublishedAt ?? _release.CreatedAt;
        public string? ReleaseBody => _release.Body;
        public bool HasAssets => Assets.Count > 0;
        #endregion

        #region Overridden Properties
        /// <summary>
        /// Gets the display name for the release
        /// </summary>
        public override string DisplayName => $"{Name} ({TagName})";
        
        /// <summary>
        /// Gets the date for sorting
        /// </summary>
        public override DateTime SortDate => _release.PublishedAt ?? _release.CreatedAt;

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

        /// <summary>
        /// Loads the assets for this release
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
                _logger.LogDebug("Loading assets for release {ReleaseId}", Id);
                
                var assetViewModels = new List<GitHubReleaseAssetViewModel>();
                
                if (_release.Assets != null)
                {
                    foreach (var asset in _release.Assets)
                    {
                        try
                        {
                            var assetViewModel = new GitHubReleaseAssetViewModel(
                                asset, this, _gitHubService, _logger);
                            assetViewModels.Add(assetViewModel);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating asset view model for asset {AssetId}", asset.Id);
                        }
                    }
                }

                await UpdateUIWithAssets(assetViewModels);

                _assetsLoaded = true;
                ChildrenLoaded = true;
                
                _logger.LogInformation("Successfully loaded {Count} assets for release {ReleaseId}", 
                    assetViewModels.Count, Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assets for release {ReleaseId}", Id);
                await ResetAssetsOnError();
            }
            finally
            {
                IsLoadingAssets = false;
            }
        }

        /// <summary>
        /// Updates the UI with asset view models
        /// </summary>
        private async Task UpdateUIWithAssets(List<GitHubReleaseAssetViewModel> assetViewModels)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    Assets.Clear();
                    Children.Clear();
                    
                    foreach (var asset in assetViewModels)
                    {
                        if (asset != null)
                        {
                            Assets.Add(asset);
                            Children.Add(asset);
                        }
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
        /// Gets a value indicating whether this release can be installed
        /// </summary>
        public override bool CanInstall => Assets.Any(a => a.CanDownload) && !string.IsNullOrEmpty(_release.TagName);

        /// <summary>
        /// Gets a value indicating whether this release can be downloaded
        /// </summary>
        public override bool CanDownload => Assets.Any(a => a.CanDownload);

        /// <summary>
        /// Command to install the release (downloads all assets)
        /// </summary>
        [RelayCommand]
        private async Task InstallReleaseAsync()
        {
            _logger.LogInformation("Installing release {ReleaseName}", _release.Name);
            
            foreach (var asset in Assets.OfType<GitHubReleaseAssetViewModel>().Where(a => a.CanDownload))
            {
                // Call the download command directly
                if (asset.DownloadAssetCommand?.CanExecute(null) == true)
                {
                    asset.DownloadAssetCommand.Execute(null);
                }
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets the install command
        /// </summary>
        public override ICommand? InstallCommand => InstallReleaseCommand;

        private void UpdateIconKey()
        {
            _iconKey = "ReleaseIcon";
        }

        private async Task RefreshAssetsAsync()
        {
            await LoadChildrenAsync();
        }

        /// <summary>
        /// Resets the assets and UI state in case of an error
        /// </summary>
        private async Task ResetAssetsOnError()
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    _logger.LogWarning("Resetting assets and UI state for release {ReleaseId} due to error", Id);
                    
                    Assets.Clear();
                    Children.Clear();
                    _assetCount = 0;
                    OnPropertyChanged(nameof(AssetCount));
                    OnPropertyChanged(nameof(HasAssets));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting assets for release {ReleaseId}", Id);
                }
            });
        }
    }
}
