using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing the main tree of GitHub items
    /// </summary>
    public partial class GitHubItemsTreeViewModel : ObservableObject
    {
        private readonly ILogger<GitHubItemsTreeViewModel> _logger;
        private readonly IGitHubServiceFacade _gitHubService;
        private readonly IGitHubViewDataProvider _gitHubDataProvider;
        private readonly IGitHubDisplayItemFactory _displayItemFactory;
        private CancellationTokenSource _loadingCts = new();
        private CancellationTokenSource? _searchCts;
        private CancellationTokenSource? _searchDebounceTokenSource;
        private bool _isRefreshing = false;
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private readonly TimeSpan _minRefreshInterval = TimeSpan.FromSeconds(5);

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<IGitHubDisplayItem> _gitHubItems = new();

        [ObservableProperty]
        private ObservableCollection<IGitHubDisplayItem> _filteredGitHubItems = new();

        [ObservableProperty]
        private IGitHubDisplayItem? _selectedGitHubItem;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _hasMoreWorkflowsToLoad = false;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private bool _showEmptyState = false;

        [ObservableProperty]
        private int _artifactCount = 0;

        [ObservableProperty]
        private bool _searchByCommitMessage = true;

        [ObservableProperty]
        private bool _searchByWorkflowNumber = false;

        // Current repository and workflow references
        private GitHubRepoSettings? _currentRepository;
        private WorkflowDefinitionViewModel? _currentWorkflow;
        private DisplayMode _currentDisplayMode = DisplayMode.All;
        #endregion

        public GitHubItemsTreeViewModel(
            ILogger<GitHubItemsTreeViewModel> logger,
            IGitHubServiceFacade gitHubService,
            IGitHubViewDataProvider gitHubDataProvider,
            IGitHubDisplayItemFactory displayItemFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
            _gitHubDataProvider = gitHubDataProvider ?? throw new ArgumentNullException(nameof(gitHubDataProvider));
            _displayItemFactory = displayItemFactory ?? throw new ArgumentNullException(nameof(displayItemFactory));
        }

        /// <summary>
        /// Set the current repository context
        /// </summary>
        public void SetRepository(GitHubRepoSettings repository)
        {
            _currentRepository = repository;
        }

        /// <summary>
        /// Set the current workflow context and display mode
        /// </summary>
        public void SetWorkflowContext(WorkflowDefinitionViewModel? workflow, DisplayMode displayMode)
        {
            _currentWorkflow = workflow;
            _currentDisplayMode = displayMode;
        }

        /// <summary>
        /// Load content based on current repository and workflow settings
        /// </summary>
        public async Task LoadContentAsync(bool clearExisting = false)
        {
            if (_currentRepository == null) return;
            
            try
            {
                _loadingCts.Cancel();
                _loadingCts = new CancellationTokenSource();
                
                IsLoading = true;
                StatusMessage = $"Loading {(_currentDisplayMode == DisplayMode.Releases ? "releases" : "items")}...";

                // Clear existing items if requested
                if (clearExisting)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        GitHubItems.Clear();
                        FilteredGitHubItems.Clear();
                    });
                }

                switch (_currentDisplayMode)
                {
                    case DisplayMode.Releases:
                        await LoadReleasesAsync(1);
                        break;

                    case DisplayMode.All:
                        // Load both workflows and releases for All Items view
                        await LoadWorkflowsAsync(1);
                        
                        if (!_loadingCts.IsCancellationRequested)
                        {
                            await LoadReleasesAsync(1);
                        }
                        break;

                    case DisplayMode.Workflows:
                        if (_currentWorkflow?.Path != null)
                            await LoadWorkflowsForPathAsync(_currentWorkflow.Path);
                        else
                            await LoadWorkflowsAsync(1);
                        break;

                    default:
                        await LoadWorkflowsAsync(1);
                        break;
                }

                // Apply any current search filter
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    ApplyFilters(true);
                }

                // Log success
                StatusMessage = $"Loaded {GitHubItems.Count} items";
                ShowEmptyState = !GitHubItems.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading content");
                StatusMessage = $"Error: {ex.Message}";
                ShowEmptyState = !GitHubItems.Any();
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads workflows from the API with pagination
        /// </summary>
        private async Task LoadWorkflowsAsync(int page = 1)
        {
            if (_currentRepository == null)
                return;

            try
            {
                // Use UI thread dispatcher for UI updates
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = "Loading workflows...";
                });

                // Keep track of our current page
                CurrentPage = page;

                // Get workflows from the repo
                var workflows = await _gitHubService.GetWorkflowRunsForRepositoryAsync(
                    _currentRepository,
                    page,
                    15, // Smaller batch size for testing
                    _loadingCts.Token);

                _logger.LogInformation("Loaded {Count} workflows", workflows.Count());

                // Create display items from workflows
                var displayItems = _displayItemFactory.CreateDisplayItemsFromWorkflows(
                    workflows,
                    this); // Change from null to this to provide proper context

                // Update UI on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (page == 1)
                    {
                        // First page - clear existing items
                        GitHubItems.Clear();
                    }

                    // Add new items
                    foreach (var item in displayItems)
                    {
                        GitHubItems.Add(item);
                    }

                    // Update UI state
                    HasMoreWorkflowsToLoad = workflows.Count() >= 15;
                    ShowEmptyState = !GitHubItems.Any();

                    // Apply filters to update the filtered view
                    ApplyFilters(true);

                    // Update artifact count
                    ArtifactCount = CountArtifacts();

                    // Update status
                    StatusMessage = $"Loaded {workflows.Count()} workflows";
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("LoadWorkflowsAsync canceled");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = "Loading workflows canceled";
                    ShowEmptyState = !GitHubItems.Any();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflows");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    ShowEmptyState = !GitHubItems.Any();
                });
            }
        }

        /// <summary>
        /// Loads releases from the GitHub API
        /// </summary>
        private async Task LoadReleasesAsync(int page = 1)
        {
            if (_currentRepository == null || (_loadingCts.IsCancellationRequested))
            {
                return;
            }

            try
            {
                // Don't set IsLoading=true here if we're loading as part of "All Items"
                // This way we don't reset the IsLoading state set by the parent method
                bool isStandaloneOperation = _currentDisplayMode == DisplayMode.Releases;
                if (isStandaloneOperation)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        StatusMessage = "Loading releases...";
                    });
                }

                // When loading releases for "All Items", don't clear existing workflow items
                if (page == 1 && _currentDisplayMode == DisplayMode.Releases)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        // Clear existing release items only when specifically in Releases mode
                        var nonReleaseItems = GitHubItems.Where(i => !i.IsRelease).ToList();
                        GitHubItems.Clear();

                        // Re-add non-release items
                        foreach (var item in nonReleaseItems)
                        {
                            GitHubItems.Add(item);
                        }
                    });
                }

                // Get releases from data provider
                var releases = await _gitHubDataProvider.GetReleasesForDisplayAsync(
                    _currentRepository,
                    page,
                    30,
                    true, // Include prereleases
                    _loadingCts.Token);

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (releases != null && releases.Any())
                    {
                        _logger.LogInformation("Loaded {Count} releases", releases.Count());

                        // Create display items for releases
                        foreach (var release in releases)
                        {
                            var releaseViewModel = _displayItemFactory.CreateFromRelease(release);

                            // Add the release to the display items
                            GitHubItems.Add(releaseViewModel);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No releases found");
                    }

                    // Apply filters
                    ApplyFilters();

                    // Update empty state
                    ShowEmptyState = GitHubItems.Count == 0;

                    if (isStandaloneOperation)
                    {
                        StatusMessage = "Ready";
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading releases");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// Loads workflows for a specific path
        /// </summary>
        private async Task LoadWorkflowsForPathAsync(string path, int page = 1)
        {
            if (_currentRepository == null || string.IsNullOrEmpty(path))
            {
                await LoadWorkflowsAsync(page);
                return;
            }

            try
            {
                _logger.LogInformation("Loading workflows for file: {Path}", path);

                // Clear the current items before loading new ones
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GitHubItems.Clear();
                });

                // Get workflows for the specific file path
                var workflows = await _gitHubDataProvider.GetWorkflowsAsync(
                    _currentRepository,
                    path,
                    page,
                    30,
                    _loadingCts.Token);

                if (workflows != null && workflows.Any())
                {
                    // Create view models
                    var viewModels = workflows.Select(w => _displayItemFactory.CreateFromWorkflow(w)).ToList();

                    // Add to UI collections on UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var vm in viewModels)
                        {
                            GitHubItems.Add(vm);
                        }

                        // Copy to filtered collection
                        FilteredGitHubItems.Clear();
                        foreach (var item in GitHubItems)
                        {
                            FilteredGitHubItems.Add(item);
                        }

                        ArtifactCount = CountArtifacts();
                        HasMoreWorkflowsToLoad = workflows.Count() >= 30;
                        CurrentPage = page;

                        ShowEmptyState = !GitHubItems.Any();
                    });
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ShowEmptyState = true;
                        ArtifactCount = 0;
                        HasMoreWorkflowsToLoad = false;
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflows for path: {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// Refreshes the content
        /// </summary>
        [RelayCommand]
        public async Task RefreshAsync()
        {
            // Only continue if sufficient time has passed since last refresh
            if (_isRefreshing || (DateTime.Now - _lastRefreshTime) < _minRefreshInterval)
            {
                _logger.LogInformation("Skipping refresh due to recent activity or already refreshing");
                return;
            }

            if (IsLoading) return;

            try
            {
                _isRefreshing = true;
                _lastRefreshTime = DateTime.Now;

                // Load content with clear
                await LoadContentAsync(true);
                StatusMessage = "Refresh complete";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        /// <summary>
        /// Applies filtering to the GitHub items
        /// </summary>
        public void ApplyFilters(bool forceUpdate = false)
        {
            try
            {
                FilteredGitHubItems.Clear();

                // Get filtered items
                IEnumerable<IGitHubDisplayItem> filteredItems = GitHubItems;

                // Apply text filter if needed
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    string query = SearchQuery.ToLowerInvariant();
                    filteredItems = filteredItems.Where(item =>
                    {
                        // Search by display name
                        if (item.DisplayName.ToLowerInvariant().Contains(query))
                            return true;

                        // Search by workflow-specific properties
                        if (item is GitHubWorkflowDisplayItemViewModel workflow)
                        {
                            return
                                (workflow.CommitMessage?.ToLowerInvariant().Contains(query) ?? false) ||
                                (workflow.CommitSha?.ToLowerInvariant().Contains(query) ?? false) ||
                                workflow.WorkflowNumber.ToString().Contains(query);
                        }

                        // Search by release-specific properties
                        if (item is GitHubReleaseDisplayItemViewModel release)
                        {
                            return
                                (release.Body?.ToLowerInvariant().Contains(query) ?? false) ||
                                release.TagName.ToLowerInvariant().Contains(query);
                        }

                        return false;
                    });
                }

                // Sort items by date (newest first)
                filteredItems = filteredItems.OrderByDescending(item => item.SortDate);

                // Add filtered items to collection
                foreach (var item in filteredItems)
                {
                    FilteredGitHubItems.Add(item);
                }

                _logger.LogDebug("Applied filters: {Count} items match the filter", FilteredGitHubItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
            }
        }

        /// <summary>
        /// Updates the filter when search query changes
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            // Debounce logic for search
            try
            {
                if (_searchDebounceTokenSource != null)
                {
                    _searchDebounceTokenSource.Cancel();
                    _searchDebounceTokenSource.Dispose();
                }
                
                _searchDebounceTokenSource = new CancellationTokenSource();

                // Immediately apply filter for instant feedback
                ApplyFilters();

                // Optionally trigger server search after delay for more complex queries
                if (!string.IsNullOrWhiteSpace(value) && value.Length > 2)
                {
                    // Schedule server search after delay
                    Task.Delay(500, _searchDebounceTokenSource.Token)
                        .ContinueWith(t => 
                        {
                            if (!t.IsCanceled)
                            {
                                Dispatcher.UIThread.InvokeAsync(() => 
                                {
                                    // Only execute server search for significant queries
                                    if (value.Length > 3) 
                                    {
                                        ExecuteServerSearchAsync().ConfigureAwait(false);
                                    }
                                });
                            }
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in search handling");
            }
        }

        /// <summary>
        /// Executes a search on the server
        /// </summary>
        [RelayCommand]
        private async Task ExecuteServerSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || IsLoading || _currentRepository == null)
                return;

            try
            {
                IsLoading = true;
                StatusMessage = "Searching...";

                // Cancel any existing search
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();

                // Determine search criteria string
                string criteriaString;
                if (SearchByWorkflowNumber)
                    criteriaString = "WorkflowNumber";
                else if (SearchByCommitMessage)
                    criteriaString = "CommitMessage";
                else
                    criteriaString = "All";

                // Execute search with string criteria
                var searchResults = await _gitHubDataProvider.SearchWorkflowsAsync(
                    SearchQuery,
                    criteriaString,
                    _searchCts.Token);

                if (searchResults != null && searchResults.Any())
                {
                    _logger.LogInformation("Search found {Count} results", searchResults.Count());

                    // Clear existing items
                    GitHubItems.Clear();
                    FilteredGitHubItems.Clear();

                    // Create display items for search results
                    var displayItems = _displayItemFactory.CreateDisplayItemsFromWorkflows(searchResults, null); // TODO: Fix the API

                    // Add to collections
                    foreach (var item in displayItems)
                    {
                        GitHubItems.Add(item);
                        FilteredGitHubItems.Add(item);
                    }

                    StatusMessage = $"Found {searchResults.Count()} results";
                    ShowEmptyState = false;
                }
                else
                {
                    _logger.LogInformation("No search results found");
                    StatusMessage = "No results found";

                    // Clear items and show empty state
                    GitHubItems.Clear();
                    FilteredGitHubItems.Clear();
                    ShowEmptyState = true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Search was cancelled");
                StatusMessage = "Search cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing server search");
                StatusMessage = $"Search error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Toggles search by workflow number
        /// </summary>
        [RelayCommand]
        private void ToggleSearchByWorkflowNumber()
        {
            SearchByWorkflowNumber = true;
            SearchByCommitMessage = false;
        }

        /// <summary>
        /// Toggles search by commit message
        /// </summary>
        [RelayCommand]
        private void ToggleSearchByCommitMessage()
        {
            SearchByWorkflowNumber = false;
            SearchByCommitMessage = true;
        }

        /// <summary>
        /// Handles selection changes from the UI
        /// </summary>
        partial void OnSelectedGitHubItemChanged(IGitHubDisplayItem? oldValue, IGitHubDisplayItem? newValue)
        {
            try
            {
                if (newValue == null) return;
                
                // Preload children if expandable
                if (newValue.IsExpandable)
                {
                    _ = newValue.LoadChildrenAsync();
                }

                // Notify listeners of the selection change
                ItemSelected?.Invoke(this, newValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SelectedGitHubItemChanged");
            }
        }

        /// <summary>
        /// Counts the total number of artifacts in all items
        /// </summary>
        private int CountArtifacts()
        {
            int count = 0;
            
            try
            {
                foreach (var item in GitHubItems)
                {
                    if (item is GitHubWorkflowDisplayItemViewModel workflow)
                    {
                        count += workflow.ArtifactCount;
                    }
                    else if (item is GitHubReleaseDisplayItemViewModel release)
                    {
                        count += release.AssetCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting artifacts");
            }
            
            return count;
        }

        /// <summary>
        /// Event that fires when an item is selected
        /// </summary>
        public event EventHandler<IGitHubDisplayItem?>? ItemSelected;
    }
}
