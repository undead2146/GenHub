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
namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Provider for GitHub data used in UI displays
    /// </summary>
    public class GitHubViewDataProvider : IGitHubViewDataProvider
    {
        private readonly ILogger<GitHubViewDataProvider> _logger;
        private readonly IGitHubWorkflowReader _workflowReader;
        private readonly IGitHubReleaseReader _releaseReader;
        private readonly IGitHubSearchService _searchService;

        public GitHubViewDataProvider(
            ILogger<GitHubViewDataProvider> logger,
            IGitHubWorkflowReader workflowReader,
            IGitHubReleaseReader releaseReader,
            IGitHubSearchService searchService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _workflowReader = workflowReader ?? throw new ArgumentNullException(nameof(workflowReader));
            _releaseReader = releaseReader ?? throw new ArgumentNullException(nameof(releaseReader));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        }

        /// <summary>
        /// Gets workflow files from a repository by extracting unique workflows
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowFilesAsync(
            GitHubRepoSettings repository,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get workflows from the repository
                var workflows = await _workflowReader.GetWorkflowRunsForRepositoryAsync(
                    repository,
                    1,  // Page
                    100, // Larger page size to get more workflow variations
                    cancellationToken);

                // Group workflows by their path to get unique workflow definitions
                var uniqueWorkflows = workflows
                    .GroupBy(w => w.WorkflowPath)
                    .Select(g => g.First())
                    .ToList();

                _logger.LogInformation("Found {Count} unique workflow files for repository {Repository}",
                    uniqueWorkflows.Count, repository.DisplayName);

                return uniqueWorkflows;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow files for repository {Repository}", repository.DisplayName);
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        /// <summary>
        /// Gets workflows from a repository with optional filtering
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> GetWorkflowsAsync(
            GitHubRepoSettings repository,
            string? workflowPath = null,
            int page = 1,
            int perPage = 30,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var workflows = await _workflowReader.GetWorkflowRunsForRepositoryAsync(
                    repository,
                    page,
                    perPage,
                    cancellationToken);

                // Filter by workflow path if specified
                if (!string.IsNullOrEmpty(workflowPath))
                {
                    workflows = workflows.Where(w => 
                        string.Equals(w.WorkflowPath, workflowPath, StringComparison.OrdinalIgnoreCase));
                }

                return workflows;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflows for repository {Repository}", repository.DisplayName);
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }

        /// <summary>
        /// Gets releases for display
        /// </summary>
        public async Task<IEnumerable<GitHubRelease>> GetReleasesForDisplayAsync(
            GitHubRepoSettings repository,
            int page = 1,
            int perPage = 30,
            bool includePrereleases = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Now this matches the parameters required
                return await _releaseReader.GetReleasesAsync(
                    repository.RepoOwner, 
                    repository.RepoName,
                    page,
                    perPage,
                    includePrereleases,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting releases for repository {Repository}", repository.DisplayName);
                return Enumerable.Empty<GitHubRelease>();
            }
        }

        /// <summary>
        /// Searches workflows based on text and criteria
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsAsync(
            string searchText,
            string searchCriteria,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _searchService.SearchWorkflowsAsync(
                    searchText, 
                    searchCriteria,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows with text {SearchText} and criteria {SearchCriteria}", 
                    searchText, searchCriteria);
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }
    }
}
