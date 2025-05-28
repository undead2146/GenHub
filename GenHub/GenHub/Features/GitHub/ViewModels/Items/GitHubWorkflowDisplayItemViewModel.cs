using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Avalonia.Threading;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;

namespace GenHub.Features.GitHub.ViewModels
{
    /// <summary>
    /// ViewModel for a GitHub workflow run with artifact loading capabilities
    /// </summary>
    public partial class GitHubWorkflowDisplayItemViewModel : GitHubDisplayItemViewModel
    {
        private readonly GitHubWorkflow _workflow;
        private readonly IGitHubArtifactReader _artifactReader;
        private readonly ILogger _logger;
        private readonly IGitHubServiceFacade? _gitHubService;
        private GitHubManagerViewModel? _parentViewModel;
        private bool _artifactsLoaded;

        private int? _artifactCountValue;
        private string _displayName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<IGitHubDisplayItem> _artifacts = new();

        [ObservableProperty]
        private bool _isLoadingArtifacts;

        /// <summary>
        /// Initializes a new instance of the GitHubWorkflowDisplayItemViewModel class
        /// </summary>
        /// <param name="workflow">The GitHub workflow data</param>
        /// <param name="artifactReader">Service for reading artifacts</param>
        /// <param name="gitHubService">The GitHub service facade</param>
        /// <param name="logger">Logger for diagnostics</param>
        public GitHubWorkflowDisplayItemViewModel(
            GitHubWorkflow workflow,
            IGitHubArtifactReader artifactReader,
            IGitHubServiceFacade? gitHubService,
            ILogger logger)
        {
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
            _gitHubService = gitHubService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _iconKey = "WorkflowIcon";

            // Initialize display name
            UpdateDisplayName();
            
            _logger.LogDebug("Created GitHubWorkflowDisplayItemViewModel for workflow {WorkflowId}, run {RunId}", 
                WorkflowId, RunId);
        }

        /// <summary>
        /// Sets the parent ViewModel reference for backward compatibility with factory methods
        /// </summary>
        public void SetParentViewModel(GitHubManagerViewModel parent)
        {
            _parentViewModel = parent;
        }

        /// <summary>
        /// Updates the display name with workflow details and context
        /// </summary>
        private void UpdateDisplayName()
        {
            string workflowName = !string.IsNullOrEmpty(_workflow.Name) ? _workflow.Name : "Workflow";
            string baseTitle = $"{workflowName} #{_workflow.WorkflowNumber}";

            if (_workflow.PullRequestNumber.HasValue && !string.IsNullOrEmpty(_workflow.PullRequestTitle))
            {
                _displayName = $"{baseTitle} - PR #{_workflow.PullRequestNumber}: {_workflow.PullRequestTitle}";
            }
            else if (!string.IsNullOrEmpty(_workflow.CommitMessage))
            {
                string commitMsg = _workflow.CommitMessage;
                int newlineIndex = commitMsg.IndexOf('\n');
                if (newlineIndex > 0)
                {
                    commitMsg = commitMsg.Substring(0, newlineIndex);
                }

                if (commitMsg.Length > 60)
                {
                    commitMsg = commitMsg.Substring(0, 57) + "...";
                }

                _displayName = $"{baseTitle} - {commitMsg}";
            }
            else
            {
                _displayName = baseTitle;
                if (!string.IsNullOrEmpty(_workflow.Name))
                {
                    _displayName += $" - {_workflow.Name}";
                }
            }
        }

        #region Workflow Properties
        /// <summary>
        /// Gets the workflow run ID
        /// </summary>
        public long RunId => _workflow.RunId;
        
        /// <summary>
        /// Gets the workflow ID
        /// </summary>
        public long WorkflowId => _workflow.WorkflowId;
        
        /// <summary>
        /// Gets the workflow name
        /// </summary>
        public string WorkflowName => _workflow.Name;
        
        /// <summary>
        /// Gets the workflow number
        /// </summary>
        public int WorkflowNumber => _workflow.WorkflowNumber;
        
        /// <summary>
        /// Gets the creation date
        /// </summary>
        public DateTime CreatedAt => _workflow.CreatedAt;
        
        /// <summary>
        /// Gets the commit SHA
        /// </summary>
        public string CommitSha => _workflow.CommitSha;
        
        /// <summary>
        /// Gets the commit message
        /// </summary>
        public string? CommitMessage => _workflow.CommitMessage;
        
        /// <summary>
        /// Gets the pull request number
        /// </summary>
        public int? PullRequestNumber => _workflow.PullRequestNumber;
        
        /// <summary>
        /// Gets the pull request title
        /// </summary>
        public string? PullRequestTitle => _workflow.PullRequestTitle;
        
        /// <summary>
        /// Gets the event type that triggered the workflow
        /// </summary>
        public string? EventType => _workflow.EventType;
        
        /// <summary>
        /// Gets the workflow file path
        /// </summary>
        public string? WorkflowPath => _workflow.WorkflowPath;

        /// <summary>
        /// Gets the repository information
        /// </summary>
        public GitHubRepoSettings? RepositoryInfo => _workflow.RepositoryInfo;

        /// <summary>
        /// Gets a value indicating whether this workflow has artifacts
        /// </summary>
        public bool HasArtifacts => ArtifactCount > 0;

        /// <summary>
        /// Gets the number of artifacts for this workflow
        /// </summary>
        public int ArtifactCount => _artifactCountValue ?? Artifacts.Count;

        /// <summary>
        /// Gets the first artifact with safe null checking
        /// </summary>
        public GitHubArtifact? FirstArtifact
        {
            get
            {
                try
                {
                    return Artifacts?.OfType<GitHubArtifactDisplayItemViewModel>()
                                  ?.FirstOrDefault()?.Artifact;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error accessing first artifact for workflow {WorkflowId}", WorkflowId);
                    return null;
                }
            }
        }
        #endregion

        #region Overridden Properties
        /// <summary>
        /// Gets the display name for the workflow
        /// </summary>
        public override string DisplayName => _displayName;
        
        /// <summary>
        /// Gets the description text for the workflow
        /// </summary>
        public override string Description => $"Run #{WorkflowNumber} - {CreatedAt:g}";
        
        /// <summary>
        /// Gets a value indicating whether this item is expandable
        /// </summary>
        public override bool IsExpandable => true;
        
        /// <summary>
        /// Gets the date for sorting
        /// </summary>
        public override DateTime SortDate => CreatedAt;
        
        /// <summary>
        /// Gets a value indicating whether this is a release
        /// </summary>
        public override bool IsRelease => false;
        
        /// <summary>
        /// Gets a value indicating whether this is a workflow run
        /// </summary>
        public override bool IsWorkflowRun => true;
        #endregion

        /// <summary>
        /// Loads artifacts associated with this workflow
        /// </summary>
        public override async Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            if (_artifactsLoaded || IsLoadingArtifacts)
            {
                _logger.LogDebug("Skipping LoadChildrenAsync - already loaded or loading for workflow {WorkflowId}", WorkflowId);
                return;
            }

            IsLoadingArtifacts = true;
            
            try
            {
                _logger.LogDebug("Starting LoadChildrenAsync for workflow {WorkflowId}, Run {RunId}", WorkflowId, RunId);

                // Create a defensive copy of workflow data to prevent shared state
                var workflowContext = CreateWorkflowCopy();

                // Validate workflow context before proceeding
                if (workflowContext.RepositoryInfo == null)
                {
                    _logger.LogWarning("Repository info is null for workflow {WorkflowId}", WorkflowId);
                    return;
                }

                _logger.LogDebug("Calling artifact reader for run {RunId}", RunId);
                
                // Get artifacts using the service
                var artifacts = await _artifactReader.GetArtifactsForRunAsync(workflowContext, cancellationToken);

                _logger.LogDebug("Artifact reader returned {Count} artifacts for workflow {WorkflowId}", 
                    artifacts?.Count() ?? -1, WorkflowId);

                // Create display items with proper null checks
                var displayItems = (artifacts ?? Enumerable.Empty<GitHubArtifact>())
                    .Where(a => a != null)
                    .Select(CreateArtifactDisplayItem)
                    .Where(item => item != null)
                    .Cast<GitHubArtifactDisplayItemViewModel>()
                    .ToList();

                _logger.LogDebug("Created {Count} display items for workflow {WorkflowId}", displayItems.Count, WorkflowId);

                // Update UI on main thread
                await UpdateUIWithArtifacts(displayItems);

                _artifactsLoaded = true;
                SetLoadedState(true);
                
                _logger.LogInformation("Successfully loaded {Count} artifacts for workflow {WorkflowId}", 
                    displayItems.Count, WorkflowId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Loading artifacts for workflow {WorkflowId} was cancelled", WorkflowId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading artifacts for workflow {WorkflowId}: {Message}", WorkflowId, ex.Message);
                
                // Reset state on error
                await ResetArtifactsOnError();
            }
            finally
            {
                IsLoadingArtifacts = false;
            }
        }

        /// <summary>
        /// Creates a defensive copy of the workflow to prevent shared state issues
        /// </summary>
        private GitHubWorkflow CreateWorkflowCopy()
        {
            return new GitHubWorkflow
            {
                RunId = this.RunId,
                WorkflowId = this.WorkflowId,
                WorkflowNumber = this.WorkflowNumber,
                Name = this.WorkflowName,
                CreatedAt = this.CreatedAt,
                RepositoryInfo = this.RepositoryInfo != null ? new GitHubRepoSettings
                {
                    RepoOwner = this.RepositoryInfo.RepoOwner,
                    RepoName = this.RepositoryInfo.RepoName,
                    DisplayName = this.RepositoryInfo.DisplayName,
                    Token = this.RepositoryInfo.Token,
                    WorkflowFile = this.RepositoryInfo.WorkflowFile,
                    Branch = this.RepositoryInfo.Branch
                } : null
            };
        }

        /// <summary>
        /// Creates an artifact display item with proper error handling
        /// </summary>
        private GitHubArtifactDisplayItemViewModel? CreateArtifactDisplayItem(GitHubArtifact artifact)
        {
            try
            {
                _logger.LogTrace("Creating display item for artifact: {ArtifactName}", artifact.Name);
                return new GitHubArtifactDisplayItemViewModel(artifact, _gitHubService, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating artifact display item for {ArtifactName}", artifact?.Name);
                return null;
            }
        }

        /// <summary>
        /// Updates the UI with artifact display items
        /// </summary>
        private async Task UpdateUIWithArtifacts(List<GitHubArtifactDisplayItemViewModel> displayItems)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    _logger.LogDebug("UI Update - Clearing collections for workflow {WorkflowId}", WorkflowId);
                    
                    Artifacts.Clear();
                    Children.Clear();

                    _logger.LogDebug("UI Update - Adding {Count} items to collections", displayItems.Count);

                    foreach (var item in displayItems)
                    {
                        if (item != null)
                        {
                            _logger.LogTrace("UI Update - Adding artifact: {ArtifactName}", item.DisplayName);
                            
                            Artifacts.Add(item);
                            Children.Add(item);
                        }
                    }

                    // Update computed properties
                    _artifactCountValue = displayItems.Count;
                    OnPropertyChanged(nameof(ArtifactCount));
                    OnPropertyChanged(nameof(HasArtifacts));
                    OnPropertyChanged(nameof(FirstArtifact));

                    // Force UI refresh
                    OnPropertyChanged(nameof(Children));
                    OnPropertyChanged(nameof(Artifacts));
                    
                    _logger.LogDebug("UI Update - Final counts - Artifacts: {ArtifactsCount}, Children: {ChildrenCount}", 
                        Artifacts.Count, Children.Count);
                    
                    // Force expansion if artifacts were loaded
                    if (displayItems.Count > 0)
                    {
                        IsExpanded = true;
                        OnPropertyChanged(nameof(IsExpanded));
                        _logger.LogTrace("UI Update - Set IsExpanded = true for workflow with {Count} artifacts", displayItems.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating UI with artifacts for workflow {WorkflowId}", WorkflowId);
                }
            });
        }

        /// <summary>
        /// Resets artifact collections when an error occurs
        /// </summary>
        private async Task ResetArtifactsOnError()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    Artifacts.Clear();
                    Children.Clear();
                    _artifactCountValue = 0;
                    OnPropertyChanged(nameof(ArtifactCount));
                    OnPropertyChanged(nameof(HasArtifacts));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting artifacts on error for workflow {WorkflowId}", WorkflowId);
                }
            });
        }
    }
}
