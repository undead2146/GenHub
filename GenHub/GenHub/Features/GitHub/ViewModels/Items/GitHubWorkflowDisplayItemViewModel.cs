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
    /// ViewModel for a GitHub workflow run
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

            // Update the display name with workflow information
            UpdateDisplayName();
        }

        /// <summary>
        /// Sets the parent ViewModel reference for backward compatibility with factory methods
        /// </summary>
        public void SetParentViewModel(GitHubManagerViewModel parent)
        {
            _parentViewModel = parent;
        }

        // Update display name with workflow details
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

        // Properties from workflow
        public long RunId => _workflow.RunId;
        public long WorkflowId => _workflow.WorkflowId;
        public string WorkflowName => _workflow.Name;
        public int WorkflowNumber => _workflow.WorkflowNumber;
        public DateTime CreatedAt => _workflow.CreatedAt;
        public string CommitSha => _workflow.CommitSha;
        public string? CommitMessage => _workflow.CommitMessage;
        public int? PullRequestNumber => _workflow.PullRequestNumber;
        public string? PullRequestTitle => _workflow.PullRequestTitle;
        public string? EventType => _workflow.EventType;
        public string? WorkflowPath => _workflow.WorkflowPath;

        public GitHubRepoSettings? RepositoryInfo => _workflow.RepositoryInfo;

        public bool HasArtifacts => ArtifactCount > 0;

        // Computed artifact count - Fix: Add parentheses to call the Count method
        public int ArtifactCount => _artifactCountValue ?? Artifacts.Count;

        // Safe accessor for the first artifact
        public GitHubArtifact? FirstArtifact =>
            Artifacts.OfType<GitHubArtifactDisplayItemViewModel>()
                   .FirstOrDefault()?.Artifact;

        // Override abstract properties
        public override string DisplayName => _displayName;
        public override string Description => $"Run #{WorkflowNumber} - {CreatedAt:g}";
        public override bool IsExpandable => true;
        public override DateTime SortDate => CreatedAt;
        public override bool IsRelease => false;
        public override bool IsWorkflowRun => true;

        /// <summary>
        /// Loads artifacts associated with this workflow
        /// </summary>
        public override async Task LoadChildrenAsync(CancellationToken cancellationToken = default)
        {
            if (_artifactsLoaded || IsLoadingArtifacts)
                return;

            IsLoadingArtifacts = true;

            try
            {
                _logger.LogDebug("Loading artifacts for workflow {WorkflowId}", RunId);

                // Get artifacts from the artifact reader - use RunId directly for better consistency
                var artifacts = await _artifactReader.GetArtifactsForRunAsync(
                    new GitHubWorkflow
                    {
                        RunId = this.RunId,
                        RepositoryInfo = this.RepositoryInfo
                    },
                    cancellationToken);

                // Ensure we have the right workflow context for each artifact
                foreach (var artifact in artifacts)
                {
                    // Set workflow properties on artifacts to ensure proper association
                    artifact.WorkflowId = this.WorkflowId;
                    artifact.WorkflowNumber = this.WorkflowNumber;
                    artifact.RunId = this.RunId;
                }

                // Create display items for the artifacts
                var displayItems = artifacts.Select(a => new GitHubArtifactDisplayItemViewModel(
                    a,
                    _gitHubService,
                    _logger)).ToList();

                // Add artifacts to the collection on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Artifacts.Clear();
                    foreach (var item in displayItems)
                    {
                        Artifacts.Add(item);
                    }

                    // Also update Children collection for IGitHubDisplayItem interface
                    Children.Clear();
                    foreach (var item in displayItems)
                    {
                        Children.Add(item);
                    }

                    // Update artifact count
                    _artifactCountValue = displayItems.Count;
                    OnPropertyChanged(nameof(ArtifactCount));
                    OnPropertyChanged(nameof(HasArtifacts));
                });

                _artifactsLoaded = true;
                SetLoadedState(true);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Loading artifacts for workflow {WorkflowId} was cancelled", RunId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading artifacts for workflow {WorkflowId}", RunId);
            }
            finally
            {
                IsLoadingArtifacts = false;
            }
        }
    }
}
