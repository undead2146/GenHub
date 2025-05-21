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
    /// Factory for creating display items for GitHub objects
    /// </summary>
    public class GitHubDisplayItemFactory : IGitHubDisplayItemFactory
    {
        private readonly IGitHubArtifactReader _artifactReader;
        private readonly IGitHubServiceFacade _gitHubServiceFacade;
        private readonly ILoggerFactory _loggerFactory;
        
        /// <summary>
        /// Initializes a new instance of the GitHubDisplayItemFactory class
        /// </summary>
        public GitHubDisplayItemFactory(
            IGitHubArtifactReader artifactReader,
            IGitHubServiceFacade gitHubServiceFacade,
            ILoggerFactory loggerFactory)
        {
            _artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
            _gitHubServiceFacade = gitHubServiceFacade ?? throw new ArgumentNullException(nameof(gitHubServiceFacade));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        /// <summary>
        /// Creates a display item from a workflow
        /// </summary>
        public IGitHubDisplayItem CreateFromWorkflow(GitHubWorkflow workflow)
        {
            var logger = _loggerFactory.CreateLogger<GitHubWorkflowDisplayItemViewModel>();
            return new GitHubWorkflowDisplayItemViewModel(
                workflow, 
                _artifactReader,
                _gitHubServiceFacade,
                logger);
        }
        
        /// <summary>
        /// Creates a display item from a workflow with parent context
        /// </summary>
        public IGitHubDisplayItem CreateFromWorkflow(GitHubWorkflow workflow, object? parent)
        {
            var logger = _loggerFactory.CreateLogger<GitHubWorkflowDisplayItemViewModel>();
            var viewModel = new GitHubWorkflowDisplayItemViewModel(
                workflow, 
                _artifactReader,
                _gitHubServiceFacade,
                logger);
                
            if (parent is GitHubManagerViewModel managerViewModel)
            {
                viewModel.SetParentViewModel(managerViewModel);
            }
            
            return viewModel;
        }
        
        /// <summary>
        /// Creates display items from a collection of workflows
        /// </summary>
        public IEnumerable<IGitHubDisplayItem> CreateFromWorkflows(IEnumerable<GitHubWorkflow> workflows)
        {
            return workflows.Select(workflow => CreateFromWorkflow(workflow)).ToList();
        }
        
        /// <summary>
        /// Creates display items from a collection of workflows with parent context
        /// </summary>
        public IEnumerable<IGitHubDisplayItem> CreateFromWorkflows(IEnumerable<GitHubWorkflow> workflows, object? parent)
        {
            return workflows.Select(w => CreateFromWorkflow(w, parent)).ToList();
        }
        
        /// <summary>
        /// Creates a display item from a release
        /// </summary>
        public IGitHubDisplayItem CreateFromRelease(GitHubRelease release)
        {
            var logger = _loggerFactory.CreateLogger<GitHubReleaseDisplayItemViewModel>();
            return new GitHubReleaseDisplayItemViewModel(
                release, 
                this, 
                _gitHubServiceFacade, 
                logger);
        }
        
        /// <summary>
        /// Creates display items from a collection of artifacts
        /// </summary>
        public IEnumerable<IGitHubDisplayItem> CreateFromArtifacts(IEnumerable<GitHubArtifact> artifacts)
        {
            var logger = _loggerFactory.CreateLogger<GitHubArtifactDisplayItemViewModel>();
            return artifacts.Select((GitHubArtifact a) => new GitHubArtifactDisplayItemViewModel(
                a,
                _gitHubServiceFacade,
                logger) as IGitHubDisplayItem)
                .ToList();
        }
        
        /// <summary>
        /// Creates workflow view models from a collection of workflows
        /// </summary>
        public IEnumerable<IGitHubDisplayItem> CreateWorkflowViewModels(IEnumerable<GitHubWorkflow> workflows, object? parent)
        {
            return CreateFromWorkflows(workflows, parent);
        }
        
        /// <summary>
        /// Creates display items for workflows from a generic parent
        /// </summary>
        public IEnumerable<IGitHubDisplayItem> CreateDisplayItemsFromWorkflows(
            IEnumerable<GitHubWorkflow> workflows, 
            object? parent)
        {
            if (parent is GitHubManagerViewModel managerViewModel)
            {
                return CreateDisplayItemsFromWorkflows(workflows, managerViewModel);
            }
            
            // For other parent types, use the standard CreateFromWorkflows
            return CreateFromWorkflows(workflows, parent);
        }
        
        /// <summary>
        /// Creates display items for workflows from a manager view model
        /// </summary>
        public IEnumerable<IGitHubDisplayItem> CreateDisplayItemsFromWorkflows(
            IEnumerable<GitHubWorkflow> workflows, 
            GitHubManagerViewModel parent)
        {
            var logger = _loggerFactory.CreateLogger<GitHubWorkflowDisplayItemViewModel>();
            var result = new List<IGitHubDisplayItem>();
            
            foreach (var workflow in workflows)
            {
                var viewModel = new GitHubWorkflowDisplayItemViewModel(
                    workflow,
                    _artifactReader,
                    _gitHubServiceFacade,
                    logger);
                    
                viewModel.SetParentViewModel(parent);
                result.Add(viewModel);
            }
            
            return result;
        }
        
        /// <summary>
        /// Helper method to load children for a collection of display items
        /// </summary>
        public async Task LoadChildrenForItemsAsync(
            IEnumerable<IGitHubDisplayItem> items,
            CancellationToken cancellationToken = default)
        {
            if (items == null) return;
            
            foreach (var item in items)
            {
                if (item.IsExpandable && !item.ChildrenLoaded)
                {
                    await item.LoadChildrenAsync(cancellationToken);
                }
            }
        }
    }
}
