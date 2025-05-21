using System.Collections.Generic;
using GenHub.Core.Models;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Interfaces.GitHub
{
    /// <summary>
    /// Factory for creating display items for GitHub objects
    /// </summary>
    public interface IGitHubDisplayItemFactory
    {
        /// <summary>
        /// Creates a display item from a workflow
        /// </summary>
        IGitHubDisplayItem CreateFromWorkflow(GitHubWorkflow workflow);
        
        /// <summary>
        /// Creates a display item from a workflow with parent context
        /// </summary>
        IGitHubDisplayItem CreateFromWorkflow(GitHubWorkflow workflow, object parent);
        
        /// <summary>
        /// Creates display items from a collection of workflows
        /// </summary>
        IEnumerable<IGitHubDisplayItem> CreateFromWorkflows(IEnumerable<GitHubWorkflow> workflows);
        
        /// <summary>
        /// Creates display items from a collection of workflows with parent context
        /// </summary>
        IEnumerable<IGitHubDisplayItem> CreateFromWorkflows(IEnumerable<GitHubWorkflow> workflows, object parent);
        
        /// <summary>
        /// Creates display items from workflows with parent context
        /// </summary>
        IEnumerable<IGitHubDisplayItem> CreateDisplayItemsFromWorkflows(
            IEnumerable<GitHubWorkflow> workflows, 
            object parent);
        
        /// <summary>
        /// Creates a display item from a release
        /// </summary>
        IGitHubDisplayItem CreateFromRelease(GitHubRelease release);
        
        /// <summary>
        /// Creates display items from a collection of artifacts
        /// </summary>
        IEnumerable<IGitHubDisplayItem> CreateFromArtifacts(IEnumerable<GitHubArtifact> artifacts);
        
        /// <summary>
        /// Creates workflow view models from a collection of workflows
        /// </summary>
        IEnumerable<IGitHubDisplayItem> CreateWorkflowViewModels(IEnumerable<GitHubWorkflow> workflows, object parent);
    }
}
