using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for content filtering options
    /// </summary>
    public partial class ContentModeFilterViewModel : ObservableObject
    {
        private readonly ILogger<ContentModeFilterViewModel> _logger;
        private readonly IGitHubViewDataProvider _gitHubDataProvider;
        private readonly IGitHubRepositoryManager _repoService;
        private CancellationTokenSource _loadingCts = new CancellationTokenSource();
        private bool _selectionChangeInProgress = false;

        #region Observable Properties
        [ObservableProperty]
        private ObservableCollection<WorkflowDefinitionViewModel> _availableWorkflows = new();

        [ObservableProperty]
        private WorkflowDefinitionViewModel? _selectedWorkflow;

        [ObservableProperty]
        private DisplayMode _currentDisplayMode = DisplayMode.All;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isLoading = false;
        #endregion

        public ContentModeFilterViewModel(
            ILogger<ContentModeFilterViewModel> logger,
            IGitHubViewDataProvider gitHubDataProvider,
            IGitHubRepositoryManager repoService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gitHubDataProvider = gitHubDataProvider ?? throw new ArgumentNullException(nameof(gitHubDataProvider));
            _repoService = repoService ?? throw new ArgumentNullException(nameof(repoService));

            // Add default options
            AddDefaultWorkflowOptions();
        }

        /// <summary>
        /// Adds the default "All Items" and "Releases" options
        /// </summary>
        private void AddDefaultWorkflowOptions()
        {
            AvailableWorkflows.Clear();
            
            // Add "All Items" option
            AvailableWorkflows.Add(new WorkflowDefinitionViewModel
            {
                Name = "All Items",
                Path = string.Empty,
                DisplayName = "All Items"
            });

            // Add "Releases" option
            AvailableWorkflows.Add(new WorkflowDefinitionViewModel
            {
                Name = "GitHub Releases",
                Path = "releases",
                DisplayName = "GitHub Releases"
            });

            // Select "All Items" by default
            _selectionChangeInProgress = true;
            SelectedWorkflow = AvailableWorkflows.FirstOrDefault();
            _selectionChangeInProgress = false;
        }

        /// <summary>
        /// Loads available workflow files for a repository
        /// </summary>
        public async Task LoadWorkflowFilesForRepositoryAsync(GitHubRepoSettings repository)
        {
            if (repository == null)
            {
                _logger.LogWarning("Cannot load workflow files for null repository");
                return;
            }

            try
            {
                // Cancel any previous loading
                _loadingCts.Cancel();
                _loadingCts = new CancellationTokenSource();

                IsLoading = true;
                StatusMessage = $"Loading workflows for {repository.DisplayName}...";

                string? currentSelectionPath = SelectedWorkflow?.Path;

                // First ensure we have the default options
                await Dispatcher.UIThread.InvokeAsync(() => {
                    // Keep just the default options
                    AddDefaultWorkflowOptions();
                });

                // Check if the repository is valid
                bool isValid = await _repoService.ValidateRepositoryAsync(
                    repository.RepoOwner,
                    repository.RepoName,
                    _loadingCts.Token);

                if (!isValid)
                {
                    _logger.LogWarning("Repository {Repo} is not valid or accessible", repository.DisplayName);
                    StatusMessage = "Repository not accessible";
                    
                    // Still select the "All Items" option
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _selectionChangeInProgress = true;
                        SelectedWorkflow = AvailableWorkflows.FirstOrDefault();
                        _selectionChangeInProgress = false;
                    });
                    
                    return;
                }

                // Try to get cached workflow files first
                string cacheKey = $"workflow_files_{repository.RepoOwner}_{repository.RepoName}";
                var cachedPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GenHub", "cache", "github", "workflow_files",
                    $"{cacheKey.Replace("/", "_").Replace(":", "_")}.json");

                IEnumerable<GitHubWorkflow>? uniqueWorkflows = null;

                // Check if we have a valid cache file that's not too old
                if (File.Exists(cachedPath) &&
                    (DateTime.Now - new FileInfo(cachedPath).LastWriteTime) < TimeSpan.FromHours(2))
                {
                    try
                    {
                        _logger.LogDebug("Loading workflow files from cache");
                        string json = await File.ReadAllTextAsync(cachedPath);
                        uniqueWorkflows = JsonSerializer.Deserialize<IEnumerable<GitHubWorkflow>>(json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load workflow files from cache");
                    }
                }

                // If cache miss or error, fetch from API
                if (uniqueWorkflows == null)
                {
                    // Get unique workflow files from workflows
                    uniqueWorkflows = await _gitHubDataProvider.GetWorkflowFilesAsync(
                        repository, _loadingCts.Token);

                    // Cache the result
                    try
                    {
                        // Ensure directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(cachedPath) ?? string.Empty);

                        string json = JsonSerializer.Serialize(uniqueWorkflows);
                        await File.WriteAllTextAsync(cachedPath, json);
                        _logger.LogDebug("Saved workflow files to cache");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache workflow files");
                    }
                }

                _logger.LogInformation("Loaded {Count} workflow files for {Repository}",
                    uniqueWorkflows.Count(), repository.DisplayName);

                // Add to the collection on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var workflow in uniqueWorkflows)
                    {
                        // Skip workflows without paths
                        if (string.IsNullOrEmpty(workflow.WorkflowPath))
                            continue;

                        // Use workflow properties directly from GitHubWorkflow
                        AvailableWorkflows.Add(new WorkflowDefinitionViewModel
                        {
                            Name = !string.IsNullOrEmpty(workflow.Name) ? workflow.Name : Path.GetFileName(workflow.WorkflowPath),
                            Path = workflow.WorkflowPath,
                            DisplayName = string.IsNullOrEmpty(workflow.Name) ?
                                Path.GetFileName(workflow.WorkflowPath) :
                                workflow.Name
                        });
                    }

                    // Try to restore previous selection if it exists
                    if (!string.IsNullOrEmpty(currentSelectionPath))
                    {
                        var previousSelection = AvailableWorkflows.FirstOrDefault(w =>
                            w.Path == currentSelectionPath);

                        if (previousSelection != null)
                        {
                            _selectionChangeInProgress = true;
                            SelectedWorkflow = previousSelection;
                            _selectionChangeInProgress = false;
                        }
                        else
                        {
                            _selectionChangeInProgress = true;
                            SelectedWorkflow = AvailableWorkflows.FirstOrDefault();
                            _selectionChangeInProgress = false;
                        }
                    }
                    else
                    {
                        _selectionChangeInProgress = true;
                        SelectedWorkflow = AvailableWorkflows.FirstOrDefault();
                        _selectionChangeInProgress = false;
                    }
                    
                    StatusMessage = $"Loaded {uniqueWorkflows.Count()} workflow files";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow files");

                // Add default option if we fail
                await Dispatcher.UIThread.InvokeAsync(() => {
                    AddDefaultWorkflowOptions();
                });
                
                StatusMessage = $"Error: {ex.Message}";
                throw; // Make sure to rethrow so the calling code knows there was an error
            }
            finally
            {
                IsLoading = false; // Always set IsLoading to false when done
            }
        }

        /// <summary>
        /// Handles changes to the selected workflow
        /// </summary>
        partial void OnSelectedWorkflowChanged(WorkflowDefinitionViewModel? oldValue, WorkflowDefinitionViewModel? newValue)
        {
            if (newValue == null || _selectionChangeInProgress) 
                return;

            _logger.LogInformation("Selected workflow changed to: {Name} ({Path})",
                newValue.Name, newValue.Path);

            // Determine the display mode based on the selection
            DisplayMode previousMode = CurrentDisplayMode;
            if (newValue.Path == "releases")
            {
                CurrentDisplayMode = DisplayMode.Releases;
            }
            else if (string.IsNullOrEmpty(newValue.Path))
            {
                CurrentDisplayMode = DisplayMode.All;
            }
            else
            {
                CurrentDisplayMode = DisplayMode.Workflows;
            }

            // Notify listeners of the workflow change
            if (previousMode != CurrentDisplayMode)
            {
                DisplayModeChanged?.Invoke(this, CurrentDisplayMode);
            }
            
            WorkflowChanged?.Invoke(this, newValue);
        }

        /// <summary>
        /// Event that fires when the selected workflow changes
        /// </summary>
        public event EventHandler<WorkflowDefinitionViewModel?>? WorkflowChanged;
        
        /// <summary>
        /// Event that fires when the display mode changes
        /// </summary>
        public event EventHandler<DisplayMode>? DisplayModeChanged;
    }
}
