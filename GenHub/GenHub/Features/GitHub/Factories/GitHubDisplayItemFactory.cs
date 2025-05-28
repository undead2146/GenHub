using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.ViewModels;

namespace GenHub.Features.GitHub.Factories
{
    /// <summary>
    /// Factory for creating display items for GitHub objects with data integrity protection
    /// </summary>
    public class GitHubDisplayItemFactory : IGitHubDisplayItemFactory
    {
        private readonly IGitHubArtifactReader _artifactReader;
        private readonly IGitHubServiceFacade _gitHubServiceFacade;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<GitHubDisplayItemFactory> _logger;
        
        public GitHubDisplayItemFactory(
            IGitHubArtifactReader artifactReader,
            IGitHubServiceFacade gitHubServiceFacade,
            ILoggerFactory loggerFactory)
        {
            _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
            _gitHubServiceFacade = gitHubServiceFacade ?? throw new ArgumentNullException(nameof(gitHubServiceFacade));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<GitHubDisplayItemFactory>();
        }
        
        public IGitHubDisplayItem CreateFromWorkflow(GitHubWorkflow workflow)
        {
            if (workflow == null)
            {
                _logger.LogError("Attempted to create workflow display item from null workflow");
                throw new ArgumentNullException(nameof(workflow));
            }
            
            _logger.LogDebug("Creating workflow display item: Name='{Name}', ID={WorkflowId}, RunNumber={RunNumber}", 
                workflow.Name, workflow.WorkflowId, workflow.WorkflowNumber);
            
            var logger = _loggerFactory.CreateLogger<GitHubWorkflowDisplayItemViewModel>();
            return new GitHubWorkflowDisplayItemViewModel(workflow, _artifactReader, _gitHubServiceFacade, logger);
        }
        
        public IGitHubDisplayItem CreateFromWorkflow(GitHubWorkflow workflow, object parent)
        {
            var viewModel = CreateFromWorkflow(workflow);
            
            if (parent is GitHubManagerViewModel managerViewModel && viewModel is GitHubWorkflowDisplayItemViewModel workflowViewModel)
            {
                workflowViewModel.SetParentViewModel(managerViewModel);
            }
            
            return viewModel;
        }
        
        public IEnumerable<IGitHubDisplayItem> CreateFromWorkflows(IEnumerable<GitHubWorkflow> workflows)
        {
            if (workflows == null)
            {
                _logger.LogWarning("Attempted to create workflow display items from null collection");
                return Enumerable.Empty<IGitHubDisplayItem>();
            }
            
            var result = new List<IGitHubDisplayItem>();
            
            foreach (var workflow in workflows)
            {
                try
                {
                    if (workflow != null)
                    {
                        result.Add(CreateFromWorkflow(workflow));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating display item for workflow {WorkflowId}", workflow?.WorkflowId);
                }
            }
            
            _logger.LogDebug("Created {Count} workflow display items from {InputCount} workflows", 
                result.Count, workflows.Count());
            
            return result;
        }
        
        public IEnumerable<IGitHubDisplayItem> CreateFromWorkflows(IEnumerable<GitHubWorkflow> workflows, object parent)
        {
            if (workflows == null)
            {
                _logger.LogWarning("Workflows collection is null");
                return Enumerable.Empty<IGitHubDisplayItem>();
            }

            // Use enhanced processing with deep copying for data integrity
            return parent is GitHubManagerViewModel managerViewModel 
                ? CreateDisplayItemsFromWorkflows(workflows, managerViewModel)
                : CreateFromWorkflows(workflows).Select(item => 
                {
                    if (parent is GitHubManagerViewModel vm && item is GitHubWorkflowDisplayItemViewModel workflowVm)
                    {
                        workflowVm.SetParentViewModel(vm);
                    }
                    return item;
                });
        }
        
        public IEnumerable<IGitHubDisplayItem> CreateDisplayItemsFromWorkflows(
            IEnumerable<GitHubWorkflow> workflows, 
            object parent)
        {
            return parent is GitHubManagerViewModel managerViewModel 
                ? CreateDisplayItemsFromWorkflows(workflows, managerViewModel)
                : CreateFromWorkflows(workflows, parent);
        }
        
        public IGitHubDisplayItem CreateFromRelease(GitHubRelease release)
        {
            var logger = _loggerFactory.CreateLogger<GitHubReleaseDisplayItemViewModel>();
            return new GitHubReleaseDisplayItemViewModel(release, this, _gitHubServiceFacade, logger);
        }
        
        public IEnumerable<IGitHubDisplayItem> CreateFromArtifacts(IEnumerable<GitHubArtifact> artifacts)
        {
            if (artifacts == null)
                return Enumerable.Empty<IGitHubDisplayItem>();
                
            var logger = _loggerFactory.CreateLogger<GitHubArtifactDisplayItemViewModel>();
            return artifacts.Select(artifact => new GitHubArtifactDisplayItemViewModel(
                artifact, _gitHubServiceFacade, logger) as IGitHubDisplayItem);
        }
        
        public IEnumerable<IGitHubDisplayItem> CreateWorkflowViewModels(IEnumerable<GitHubWorkflow> workflows, object parent)
        {
            return CreateFromWorkflows(workflows, parent);
        }
        
        /// <summary>
        /// Creates display items with enhanced data integrity through deep copying
        /// </summary>
        private IEnumerable<IGitHubDisplayItem> CreateDisplayItemsFromWorkflows(
            IEnumerable<GitHubWorkflow> workflows, 
            GitHubManagerViewModel? parent)
        {
            if (workflows == null)
            {
                _logger.LogWarning("Workflows collection is null");
                return Enumerable.Empty<IGitHubDisplayItem>();
            }

            var workflowList = workflows.ToList();
            var result = new List<IGitHubDisplayItem>();
            
            _logger.LogDebug("Creating display items for {Count} workflows with data integrity protection", workflowList.Count);

            for (int i = 0; i < workflowList.Count; i++)
            {
                var workflow = workflowList[i];
                
                try
                {
                    if (workflow == null)
                    {
                        _logger.LogWarning("Workflow at index {Index} is null", i);
                        continue;
                    }

                    // Validate and potentially fix workflow data
                    if (!IsWorkflowDataValid(workflow, i))
                        continue;

                    // Create defensive copy to prevent data corruption
                    var workflowCopy = CreateDeepWorkflowCopy(workflow);
                    
                    // Create ViewModel with copied data
                    var logger = _loggerFactory.CreateLogger<GitHubWorkflowDisplayItemViewModel>();
                    var viewModel = new GitHubWorkflowDisplayItemViewModel(workflowCopy, _artifactReader, _gitHubServiceFacade, logger);
                    
                    if (parent != null)
                    {
                        viewModel.SetParentViewModel(parent);
                    }
                    
                    result.Add(viewModel);
                    
                    _logger.LogTrace("Created ViewModel for workflow {Index}: {DisplayName}", i, viewModel.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating workflow display item at index {Index} (WorkflowId: {WorkflowId})", 
                        i, workflow?.WorkflowId);
                }
            }
            
            _logger.LogDebug("Successfully created {ResultCount} display items from {InputCount} workflows", 
                result.Count, workflowList.Count);
            
            return result;
        }
        
        /// <summary>
        /// Validates workflow data integrity
        /// </summary>
        private bool IsWorkflowDataValid(GitHubWorkflow workflow, int index)
        {
            var isValid = true;
            
            if (workflow.WorkflowNumber <= 0)
            {
                _logger.LogWarning("Workflow at index {Index} has invalid WorkflowNumber: {WorkflowNumber}", 
                    index, workflow.WorkflowNumber);
                // Don't fail validation for this, just log it
            }
            
            if (workflow.CreatedAt == DateTime.MinValue || workflow.CreatedAt == default)
            {
                _logger.LogWarning("Workflow at index {Index} has invalid CreatedAt: {CreatedAt}", 
                    index, workflow.CreatedAt);
                // Don't fail validation for this, just log it
            }
            
            return isValid;
        }
        
        /// <summary>
        /// Creates a deep copy of workflow ensuring complete object isolation
        /// </summary>
        private GitHubWorkflow CreateDeepWorkflowCopy(GitHubWorkflow original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));
            
            var copy = new GitHubWorkflow
            {
                RunId = original.RunId,
                WorkflowId = original.WorkflowId,
                WorkflowNumber = original.WorkflowNumber,
                Name = original.Name,
                DisplayTitle = original.DisplayTitle,
                Status = original.Status,
                Conclusion = original.Conclusion,
                EventType = original.EventType,
                CreatedAt = original.CreatedAt,
                UpdatedAt = original.UpdatedAt,
                CommitSha = original.CommitSha,
                CommitMessage = original.CommitMessage,
                PullRequestNumber = original.PullRequestNumber,
                PullRequestTitle = original.PullRequestTitle,
                HtmlUrl = original.HtmlUrl,
                HeadBranch = original.HeadBranch,
                Actor = original.Actor,
                WorkflowPath = original.WorkflowPath,
                HasArtifacts = original.HasArtifacts,
                ArtifactCount = original.ArtifactCount,
                RepositoryInfo = CreateDeepRepositoryCopy(original.RepositoryInfo),
                Metadata = CreateDeepMetadataCopy(original.Metadata),
                Artifacts = CreateDeepArtifactsCopy(original.Artifacts)
            };
            
            return copy;
        }

        private GitHubRepoSettings? CreateDeepRepositoryCopy(GitHubRepoSettings? original)
        {
            return original == null ? null : new GitHubRepoSettings
            {
                RepoOwner = original.RepoOwner,
                RepoName = original.RepoName,
                DisplayName = original.DisplayName,
                Token = original.Token,
                WorkflowFile = original.WorkflowFile,
                Branch = original.Branch
            };
        }

        private Dictionary<string, object>? CreateDeepMetadataCopy(Dictionary<string, object>? original)
        {
            if (original == null) return null;
            
            var copy = new Dictionary<string, object>();
            foreach (var kvp in original)
            {
                copy[kvp.Key] = kvp.Value; // Note: Shallow copy of values
            }
            return copy;
        }

        private List<GitHubArtifact>? CreateDeepArtifactsCopy(List<GitHubArtifact>? original)
        {
            if (original == null) return null;
            
            return original.Select(CreateDeepArtifactCopy).Where(copy => copy != null).ToList()!;
        }

        private GitHubArtifact? CreateDeepArtifactCopy(GitHubArtifact? original)
        {
            if (original == null) return null;
            
            return new GitHubArtifact
            {
                Id = original.Id,
                Name = original.Name,
                WorkflowId = original.WorkflowId,
                RunId = original.RunId,
                WorkflowNumber = original.WorkflowNumber,
                SizeInBytes = original.SizeInBytes,
                IsRelease = original.IsRelease,
                DownloadUrl = original.DownloadUrl,
                ArchiveDownloadUrl = original.ArchiveDownloadUrl,
                Expired = original.Expired,
                CreatedAt = original.CreatedAt,
                ExpiresAt = original.ExpiresAt,
                PullRequestNumber = original.PullRequestNumber,
                PullRequestTitle = original.PullRequestTitle,
                CommitSha = original.CommitSha,
                CommitMessage = original.CommitMessage,
                EventType = original.EventType,
                BuildPreset = original.BuildPreset,
                IsActive = original.IsActive,
                IsInstalled = original.IsInstalled,
                IsInstalling = original.IsInstalling,
                BuildInfo = CreateDeepBuildInfoCopy(original.BuildInfo),
                RepositoryInfo = CreateDeepRepositoryCopy(original.RepositoryInfo)
            };
        }

        private GitHubBuild? CreateDeepBuildInfoCopy(GitHubBuild? original)
        {
            return original == null ? null : new GitHubBuild
            {
                GameVariant = original.GameVariant,
                Compiler = original.Compiler,
                Configuration = original.Configuration,
                HasTFlag = original.HasTFlag,
                HasEFlag = original.HasEFlag
            };
        }
        
        /// <summary>
        /// Helper method to load children for a collection of display items
        /// </summary>
        public async Task LoadChildrenForItemsAsync(
            IEnumerable<IGitHubDisplayItem> items,
            CancellationToken cancellationToken = default)
        {
            if (items == null) return;
            
            var tasks = items
                .Where(item => item.IsExpandable && !item.ChildrenLoaded)
                .Select(item => item.LoadChildrenAsync(cancellationToken));
                
            await Task.WhenAll(tasks);
        }
    }
}
