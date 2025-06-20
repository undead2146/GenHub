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
using GenHub.Core.Models.GitHub;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for managing the main tree of GitHub items with enhanced search capabilities
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

        #region Observable Properties - Original Properties
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
        #endregion

        #region Observable Properties - New Search Properties
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private GitHubSearchCriteria _selectedSearchCriteria = GitHubSearchCriteria.All;

        [ObservableProperty]
        private bool _isSearchActive = false;

        [ObservableProperty]
        private int _searchResultCount = 0;
        #endregion

        // Current repository and workflow references
        private GitHubRepository? _currentRepository;
        private WorkflowDefinitionViewModel? _currentWorkflow;
        private DisplayMode _currentDisplayMode = DisplayMode.All;
        
        // Private backing fields
        private ObservableCollection<IGitHubDisplayItem> _items = new();
        private GitHubManagerViewModel? _parentViewModel;

        #region Events
        public event EventHandler<IGitHubDisplayItem?>? ItemSelected;
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

            // Available search criteria for UI binding
            SearchCriteriaOptions = Enum.GetValues<GitHubSearchCriteria>().ToList();
        }

        #region Properties
        /// <summary>
        /// Available search criteria options for UI binding
        /// </summary>
        public List<GitHubSearchCriteria> SearchCriteriaOptions { get; }

        /// <summary>
        /// Gets whether search functionality is available
        /// </summary>
        public bool CanSearch => _currentRepository != null && !IsLoading;

        /// <summary>
        /// Gets the items to display in the tree
        /// </summary>
        public ObservableCollection<IGitHubDisplayItem> Items
        {
            get
            {
                _logger.LogTrace("Items getter called. Count: {Count}", _items?.Count ?? -1);
                return _items;
            }
            private set
            {
                _logger.LogDebug("Setting Items collection. Old count: {OldCount}, New count: {NewCount}", 
                    _items?.Count ?? -1, value?.Count ?? -1);
                    
                if (value != null && value.Any())
                {
                    for (int i = 0; i < Math.Min(3, value.Count); i++)
                    {
                        var item = value[i];
                        _logger.LogDebug("New Item {Index}: Type={Type}, DisplayName='{DisplayName}'", 
                            i, item?.GetType().Name, item?.DisplayName);
                    }
                }
                
                SetProperty(ref _items, value);
                
                // Monitor collection changes
                if (_items != null)
                {
                    _items.CollectionChanged += (sender, args) =>
                    {
                        _logger.LogDebug("Items collection changed. Action: {Action}, NewItems: {NewCount}, OldItems: {OldCount}", 
                            args.Action, args.NewItems?.Count ?? 0, args.OldItems?.Count ?? 0);
                    };
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether there are items to display
        /// </summary>
        public bool HasItems => Items?.Count > 0;
        #endregion

        #region Context Management
        /// <summary>
        /// Sets the repository context for this tree view
        /// </summary>
        public void SetRepository(GitHubRepository repository)
        {
            _currentRepository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger.LogInformation("Repository context set to: {Repository}", repository.DisplayName);
            
            // Clear search when repository changes
            ClearSearch();
        }

        /// <summary>
        /// Sets the workflow context and display mode
        /// </summary>
        public void SetWorkflowContext(WorkflowDefinitionViewModel? workflow, DisplayMode displayMode)
        {
            _currentWorkflow = workflow;
            _currentDisplayMode = displayMode;
            
            _logger.LogInformation("Workflow context set: Workflow='{WorkflowName}', Mode={DisplayMode}",
                workflow?.Name ?? "All Items", displayMode);
                
            // Clear search when context changes
            ClearSearch();
        }
        #endregion

        #region Commands
        /// <summary>
        /// Command for performing search
        /// </summary>
        [RelayCommand]
        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText) && string.IsNullOrWhiteSpace(SearchQuery))
            {
                await ClearSearchAsync();
                return;
            }

            try
            {
                // Use existing ExecuteServerSearchAsync logic
                await ExecuteServerSearchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchAsync");
            }
        }

        /// <summary>
        /// Command for clearing search
        /// </summary>
        [RelayCommand]
        public async Task ClearSearchAsync()
        {
            try
            {
                SearchText = string.Empty;
                SearchQuery = string.Empty;
                IsSearchActive = false;
                SearchResultCount = 0;
                
                // Reload content
                await LoadContentAsync(clearExisting: true, forceRefresh: true);
                
                _logger.LogInformation("Search cleared, default content restored");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing search");
            }
        }
        #endregion

        #region Enhanced Search Implementation
        /// <summary>
        /// Clears search without triggering reload (for context changes)
        /// </summary>
        private void ClearSearch()
        {
            SearchText = string.Empty;
            IsSearchActive = false;
            SearchResultCount = 0;
        }
        #endregion

        #region Legacy Search Support
        /// <summary>
        /// Executes a search on the server (legacy method)
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

                // Execute search with string criteria (legacy API)
                var searchResults = await _gitHubDataProvider.SearchWorkflowsAsync(
                    SearchQuery,
                    criteriaString,
                    _searchCts.Token);

                if (searchResults != null && searchResults.Any())
                {
                    _logger.LogInformation("Legacy search found {Count} results", searchResults.Count());

                    // Clear existing items
                    GitHubItems.Clear();
                    FilteredGitHubItems.Clear();
                    Items.Clear();

                    // Create display items for search results
                    var displayItems = _displayItemFactory.CreateDisplayItemsFromWorkflows(searchResults, this);

                    // Add to collections
                    foreach (var item in displayItems)
                    {
                        GitHubItems.Add(item);
                        FilteredGitHubItems.Add(item);
                        Items.Add(item);
                    }

                    StatusMessage = $"Found {searchResults.Count()} results";
                    ShowEmptyState = false;
                }
                else
                {
                    _logger.LogInformation("No legacy search results found");
                    StatusMessage = "No results found";

                    // Clear items and show empty state
                    GitHubItems.Clear();
                    FilteredGitHubItems.Clear();
                    Items.Clear();
                    ShowEmptyState = true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Legacy search was cancelled");
                StatusMessage = "Search cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing legacy server search");
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
        #endregion

        #region Content Loading (Original Implementation)
        /// <summary>
        /// Load content based on current repository and workflow settings
        /// </summary>
        public async Task LoadContentAsync(bool clearExisting = false, bool forceRefresh = false)
        {
            if (_currentRepository == null)
            {
                _logger.LogWarning("Cannot load content without repository context");
                return;
            }

            // If search is active, don't reload default content unless forced
            if (IsSearchActive && !forceRefresh)
            {
                return;
            }
            
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
                        Items.Clear();
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

        private async Task LoadWorkflowsAsync(int page = 1)
        {
            if (_currentRepository == null)
                return;

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (page == 1)
                    {
                        GitHubItems.Clear();
                        FilteredGitHubItems.Clear();
                        Items.Clear();
                    }
                });

                CurrentPage = page;

                var workflows = await _gitHubService.GetWorkflowRunsForRepositoryAsync(
                    _currentRepository,
                    page,
                    15,
                    _loadingCts.Token);

                _logger.LogInformation("Loaded {Count} workflows", workflows.Count());

                var displayItems = _displayItemFactory.CreateFromWorkflows(workflows, _parentViewModel);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (page == 1)
                    {
                        GitHubItems.Clear();
                        FilteredGitHubItems.Clear();
                        Items.Clear();
                    }

                    foreach (var item in displayItems)
                    {
                        GitHubItems.Add(item);
                        FilteredGitHubItems.Add(item);
                        Items.Add(item);
                    }

                    OnPropertyChanged(nameof(HasItems));
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("LoadWorkflowsAsync canceled");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = "Loading cancelled";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflows");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                });
            }
        }

        private async Task LoadReleasesAsync(int page = 1)
        {
            if (_currentRepository == null || (_loadingCts.IsCancellationRequested))
            {
                return;
            }

            try
            {
                bool isStandaloneOperation = _currentDisplayMode == DisplayMode.Releases;
                if (isStandaloneOperation)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        GitHubItems.Clear();
                        FilteredGitHubItems.Clear();
                        Items.Clear();
                    });
                }

                if (page == 1 && _currentDisplayMode == DisplayMode.Releases)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        GitHubItems.Clear();
                        FilteredGitHubItems.Clear();
                        Items.Clear();
                    });
                }

                var releases = await _gitHubDataProvider.GetReleasesForDisplayAsync(
                    _currentRepository,
                    page,
                    30,
                    true,
                    _loadingCts.Token);

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (releases != null && releases.Any())
                    {
                        foreach (var release in releases)
                        {
                            // Create display items from releases using the factory
                            var displayItem = _displayItemFactory.CreateFromRelease(release);
                            GitHubItems.Add(displayItem);
                            FilteredGitHubItems.Add(displayItem);
                            Items.Add(displayItem);
                        }
                        
                        OnPropertyChanged(nameof(HasItems));
                    }
                    else
                    {
                        if (isStandaloneOperation)
                        {
                            ShowEmptyState = true;
                        }
                    }

                    if (isStandaloneOperation)
                    {
                        StatusMessage = $"Loaded {releases?.Count() ?? 0} releases";
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading releases");
                await Dispatcher.UIThread.InvokeAsync(() => 
                {
                    StatusMessage = $"Error loading releases: {ex.Message}";
                });
            }
        }

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

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    GitHubItems.Clear();
                    FilteredGitHubItems.Clear();
                    Items.Clear();
                });

                var workflows = await _gitHubDataProvider.GetWorkflowsAsync(
                    _currentRepository,
                    path,
                    page,
                    30,
                    _loadingCts.Token);

                if (workflows != null && workflows.Any())
                {
                    var viewModels = workflows.Select(w => _displayItemFactory.CreateFromWorkflow(w)).ToList();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var vm in viewModels)
                        {
                            GitHubItems.Add(vm);
                            FilteredGitHubItems.Add(vm);
                            Items.Add(vm);
                        }
                        
                        OnPropertyChanged(nameof(HasItems));
                        StatusMessage = $"Loaded {viewModels.Count} workflows";
                    });
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ShowEmptyState = true;
                        StatusMessage = "No workflows found";
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflows for path: {Path}", path);
                throw;
            }
        }
        #endregion

        // ...existing code for RefreshAsync, ApplyFilters, UpdateDisplayAsync, etc...
        [RelayCommand]
        public async Task RefreshAsync()
        {
            if (_isRefreshing || (DateTime.Now - _lastRefreshTime) < _minRefreshInterval)
            {
                _logger.LogInformation("Skipping refresh due to recent activity or already refreshing");
                return;
            }

            if (IsLoading) return;

            // If search is active, re-run search instead of loading default content
            if (IsSearchActive)
            {
                // Don't call SearchAsync() - let the existing search handling work
                return;
            }

            try
            {
                _isRefreshing = true;
                _lastRefreshTime = DateTime.Now;

                await LoadContentAsync(clearExisting: true, forceRefresh: true);
                StatusMessage = "Refresh complete";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        public void ApplyFilters(bool forceUpdate = false)
        {
            try
            {
                FilteredGitHubItems.Clear();

                IEnumerable<IGitHubDisplayItem> filteredItems = GitHubItems;

                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    string query = SearchQuery.ToLowerInvariant();
                    filteredItems = filteredItems.Where(item =>
                    {
                        if (item.DisplayName.ToLowerInvariant().Contains(query))
                            return true;

                        if (item is GitHubWorkflowDisplayItemViewModel workflow)
                        {
                            return
                                (workflow.CommitMessage?.ToLowerInvariant().Contains(query) ?? false) ||
                                (workflow.CommitSha?.ToLowerInvariant().Contains(query) ?? false) ||
                                workflow.WorkflowNumber.ToString().Contains(query);
                        }

                        if (item is GitHubReleaseDisplayItemViewModel release)
                        {
                            return
                                (release.Body?.ToLowerInvariant().Contains(query) ?? false) ||
                                release.TagName.ToLowerInvariant().Contains(query);
                        }

                        return false;
                    });
                }

                filteredItems = filteredItems.OrderByDescending(item => item.SortDate);

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

        public async Task UpdateDisplayAsync(
            IEnumerable<GitHubWorkflow>? workflows = null,
            IEnumerable<GitHubRelease>? releases = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("UpdateDisplayAsync called. Workflows: {WorkflowCount}, Releases: {ReleaseCount}", 
                    workflows?.Count() ?? 0, releases?.Count() ?? 0);
                
                IsLoading = true;
                var displayItems = new List<IGitHubDisplayItem>();

                if (workflows != null && workflows.Any())
                {
                    _logger.LogDebug("Processing {Count} workflows through factory", workflows.Count());
                    
                    var workflowItems = _displayItemFactory.CreateFromWorkflows(workflows, _parentViewModel);
                    displayItems.AddRange(workflowItems);
                    
                    _logger.LogDebug("Factory created {Count} workflow display items", workflowItems.Count());
                }

                if (releases != null && releases.Any())
                {
                    _logger.LogDebug("Processing {Count} releases", releases.Count());
                    
                    var releaseItems = releases.Select(r => _displayItemFactory.CreateFromRelease(r));
                    displayItems.AddRange(releaseItems);
                    
                    _logger.LogDebug("Created {Count} release display items", releaseItems.Count());
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        _logger.LogDebug("Updating Items collection on UI thread with {Count} items", displayItems.Count);
                        
                        var oldCount = Items.Count;
                        Items.Clear();
                        
                        foreach (var item in displayItems)
                        {
                            if (item != null)
                            {
                                Items.Add(item);
                            }
                            else
                            {
                                _logger.LogWarning("Skipping null item in display update");
                            }
                        }
                        
                        _logger.LogDebug("Items collection updated. Old count: {OldCount}, New count: {NewCount}", 
                            oldCount, Items.Count);
                        
                        OnPropertyChanged(nameof(Items));
                        OnPropertyChanged(nameof(HasItems));
                        
                        if (!Items.Any() && displayItems.Any())
                        {
                            _logger.LogWarning("Items collection is empty after update despite having display items");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating Items collection on UI thread");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateDisplayAsync");
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            try
            {
                if (_searchDebounceTokenSource != null)
                {
                    _searchDebounceTokenSource.Cancel();
                    _searchDebounceTokenSource.Dispose();
                }
                
                _searchDebounceTokenSource = new CancellationTokenSource();

                ApplyFilters();

                if (!string.IsNullOrWhiteSpace(value) && value.Length > 2)
                {
                    Task.Delay(500, _searchDebounceTokenSource.Token)
                        .ContinueWith(t => 
                        {
                            if (!t.IsCanceled)
                            {
                                Dispatcher.UIThread.InvokeAsync(() => 
                                {
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
        /// Handles selection changes from the UI
        /// </summary>
        partial void OnSelectedGitHubItemChanged(IGitHubDisplayItem? oldValue, IGitHubDisplayItem? newValue)
        {
            try
            {
                if (newValue == null) return;
                
                _logger.LogDebug("Item selected: {DisplayName} (Type: {Type})", 
                    newValue.DisplayName, newValue.GetType().Name);
                
                // Preload children if expandable and not already loaded
                if (newValue.IsExpandable && !newValue.ChildrenLoaded)
                {
                    _ = newValue.LoadChildrenAsync();
                }

                // Notify parent viewmodel of selection change
                ItemSelected?.Invoke(this, newValue);
                
                // Update the details view through the parent
                if (_parentViewModel != null)
                {
                    // The parent will handle updating details and installation views
                    _logger.LogDebug("Notifying parent of item selection");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item selection");
            }
        }

        /// <summary>
        /// Command to select an item and raise the ItemSelected event
        /// </summary>
        [RelayCommand]
        private void SelectItem(IGitHubDisplayItem item)
        {
            if (item != null)
            {
                SelectedGitHubItem = item;
                _logger.LogDebug("Item selected via command: {DisplayName}", item.DisplayName);
            }
        }

        /// <summary>
        /// Sets the parent ViewModel for coordination
        /// </summary>
        public void SetParentViewModel(GitHubManagerViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            _logger.LogDebug("Parent ViewModel set");
        }

        /// <summary>
        /// Gets the current workflow path for context-aware operations
        /// </summary>
        /// <returns>The workflow path or null for "All Items" mode</returns>
        public string? GetCurrentWorkflowPath()
        {
            if (_currentDisplayMode == DisplayMode.All)
                return null; // All Items mode
                
            return _currentWorkflow?.Path; // Specific workflow path or null
        }

        public void Dispose()
        {
            _loadingCts?.Cancel();
            _loadingCts?.Dispose();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchDebounceTokenSource?.Cancel();
            _searchDebounceTokenSource?.Dispose();
        }
    }
}
