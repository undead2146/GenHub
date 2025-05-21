using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces;
using Microsoft.Extensions.Logging;
using GenHub.Core.Models;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
namespace GenHub.Features.GitHub.Services
{
    /// <summary>
    /// Service for searching GitHub resources
    /// </summary>
    public class GitHubSearchService : IGitHubSearchService
    {
        private readonly IGitHubApiClient _apiClient;
        private readonly IGitHubWorkflowReader _workflowService;
        private readonly IGitHubRepositoryManager _repoService;
        private readonly ILogger<GitHubSearchService> _logger;

        public GitHubSearchService(
            IGitHubApiClient apiClient,
            IGitHubWorkflowReader workflowService,
            IGitHubRepositoryManager repoService,
            ILogger<GitHubSearchService> logger)
        {
            _apiClient = apiClient;
            _workflowService = workflowService;
            _repoService = repoService;
            _logger = logger;
        }

        /// <summary>
        /// Searches for workflow runs by pull request number
        /// </summary>
        public async Task<List<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(int pullRequestNumber, CancellationToken cancellationToken = default)
        {
            var results = await SearchWorkflowsByPullRequestAsync(_repoService.GetDefaultRepository(), pullRequestNumber, cancellationToken);
            return results.ToList();
        }

        /// <summary>
        /// Searches for workflow runs by pull request number in a specific repository
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByPullRequestAsync(GitHubRepoSettings repository, int pullRequestNumber, CancellationToken cancellationToken = default)
        {
            var path = $"repos/{repository.RepoOwner}/{repository.RepoName}/actions/runs?event=pull_request&per_page=100";
            Console.WriteLine($"Searching workflows by PR {pullRequestNumber} using path: {path}");

            try
            {
                // Use the existing GitHubApiResponse class or create a new one
                var apiResponse = await _apiClient.GetAsync<GitHubWorkflowResponse>(path, cancellationToken);

                if (apiResponse?.WorkflowRuns == null)
                    return new List<GitHubWorkflow>();

                var workflowRuns = new List<GitHubWorkflow>();

                foreach (var workflowData in apiResponse.WorkflowRuns)
                {
                    // Check if this workflow is for the PR we're looking for
                    if (workflowData.PullRequests != null &&
                        workflowData.PullRequests.Any(pr => pr.Number == pullRequestNumber))
                    {
                        var workflow = ConvertToWorkflow(workflowData, repository);
                        workflowRuns.Add(workflow);
                    }
                }

                Console.WriteLine($"Found {workflowRuns.Count} workflows for PR {pullRequestNumber}");
                return workflowRuns;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching workflows by PR {pullRequestNumber}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Searches workflow runs by text in commit messages, titles, etc.
        /// </summary>
        public async Task<IEnumerable<GitHubWorkflow>> SearchWorkflowsByTextAsync(GitHubRepoSettings repository, string searchText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return Array.Empty<GitHubWorkflow>();

            var path = $"repos/{repository.RepoOwner}/{repository.RepoName}/actions/runs?per_page=100";
            Console.WriteLine($"Searching workflows by text '{searchText}' using path: {path}");

            try
            {
                // Use the new GetRawAsync method to get raw HTTP response
                var response = await _apiClient.GetRawAsync(repository, path, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Parse the JSON response
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var document = JsonDocument.Parse(jsonContent);
                var workflowRuns = new List<GitHubWorkflow>();

                // Check if the workflow_runs property exists
                if (document.RootElement.TryGetProperty("workflow_runs", out var workflowsElement))
                {
                    searchText = searchText.ToLowerInvariant();

                    // Iterate through workflow runs
                    foreach (var workflowElement in workflowsElement.EnumerateArray())
                    {
                        // Check name (display name of the workflow)
                        bool nameMatches = false;
                        if (workflowElement.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString()?.ToLowerInvariant() ?? string.Empty;
                            nameMatches = name.Contains(searchText);
                        }

                        // Check display_title (often includes branch info)
                        bool titleMatches = false;
                        if (workflowElement.TryGetProperty("display_title", out var displayTitleElement))
                        {
                            var title = displayTitleElement.GetString()?.ToLowerInvariant() ?? string.Empty;
                            titleMatches = title.Contains(searchText);
                        }

                        // Check head_commit message
                        bool commitMatches = false;
                        if (workflowElement.TryGetProperty("head_commit", out var headCommitElement) &&
                            headCommitElement.TryGetProperty("message", out var messageElement))
                        {
                            var message = messageElement.GetString()?.ToLowerInvariant() ?? string.Empty;
                            commitMatches = message.Contains(searchText);
                        }

                        if (nameMatches || titleMatches || commitMatches)
                        {
                            // This workflow matches our search text
                            var workflow = ParseWorkflowRunFromJson(workflowElement, repository);
                            workflowRuns.Add(workflow);

                            Console.WriteLine($"Found matching workflow: {workflow.Name}, CommitMessage: {workflow.CommitMessage?.Substring(0, Math.Min(20, workflow.CommitMessage?.Length ?? 0))}");
                        }
                    }
                }

                Console.WriteLine($"Found {workflowRuns.Count} workflows matching '{searchText}'");
                return workflowRuns;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching workflows by text '{searchText}': {ex.Message}");
                throw;
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
                _logger.LogInformation("Searching workflows with text '{SearchText}' and criteria '{SearchCriteria}'",
                    searchText, searchCriteria);

                // Get default repository
                var defaultRepo = _repoService.GetDefaultRepository();

                // Use appropriate search strategy based on criteria
                switch (searchCriteria.ToLowerInvariant())
                {
                    case "pr":
                    case "pullrequest":
                        if (int.TryParse(searchText, out int prNumber))
                        {
                            return await SearchWorkflowsByPullRequestAsync(prNumber, cancellationToken);
                        }
                        break;

                    case "workflow":
                    case "number":
                        if (int.TryParse(searchText, out int workflowNumber))
                        {
                            return await SearchWorkflowsByWorkflowNumberAsync(workflowNumber, cancellationToken);
                        }
                        break;

                    case "commit":
                        return await SearchWorkflowsByCommitMessageAsync(searchText, cancellationToken);

                    case "text":
                    default:
                        // Default to text search in all fields
                        return await SearchWorkflowsByTextAsync(defaultRepo, searchText, cancellationToken);
                }

                // If we reach here, the criteria or text format wasn't valid
                return Enumerable.Empty<GitHubWorkflow>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching workflows with text '{SearchText}' and criteria '{SearchCriteria}'",
                    searchText, searchCriteria);
                return Enumerable.Empty<GitHubWorkflow>();
            }
        }
        /// <summary>
        /// Searches workflows by workflow run number
        /// </summary>
        public async Task<List<GitHubWorkflow>> SearchWorkflowsByWorkflowNumberAsync(
            int workflowNumber,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Searching for workflow with number: {WorkflowNumber}", workflowNumber);

                // Get default repository
                var defaultRepo = _repoService.GetDefaultRepository();

                try
                {
                    // Use existing API to get workflow runs and filter by workflow number
                    // This is more efficient than getting all workflows individually
                    var allWorkflows = await _workflowService.GetWorkflowRunsForRepositoryAsync(
                        defaultRepo,
                        1,  // Start with page 1
                        100, // Increase per page to reduce API calls
                        cancellationToken);

                    // Filter workflows by the workflow number
                    var matchingWorkflows = allWorkflows
                        .Where(w => w.WorkflowNumber == workflowNumber)
                        .ToList();

                    if (matchingWorkflows.Any())
                    {
                        _logger.LogInformation("Found {Count} workflow runs with number {WorkflowNumber}",
                            matchingWorkflows.Count, workflowNumber);
                        return matchingWorkflows;
                    }
                    else
                    {
                        _logger.LogInformation("No workflow found with number {WorkflowNumber}", workflowNumber);
                        return new List<GitHubWorkflow>();
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No workflow found with number {WorkflowNumber}", workflowNumber);
                    return new List<GitHubWorkflow>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error searching for workflow by number: {WorkflowNumber}", workflowNumber);
                    throw new GitHubServiceException($"Error searching for workflow by number: {workflowNumber}", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchWorkflowsByWorkflowNumberAsync: {WorkflowNumber}", workflowNumber);
                throw new GitHubServiceException($"Failed to search workflow by number: {workflowNumber}", ex);
            }
        }

        /// <summary>
        /// Searches for workflow runs by commit message text
        /// </summary>
        public async Task<List<GitHubWorkflow>> SearchWorkflowsByCommitMessageAsync(
            string searchText,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Console.WriteLine($"Searching workflows by commit message '{searchText}'");
                var repository = _repoService.GetDefaultRepository();

                // Reuse the text search method with conversion to List
                var results = await SearchWorkflowsByTextAsync(repository, searchText, cancellationToken);
                return results.Where(w =>
                    !string.IsNullOrEmpty(w.CommitMessage) &&
                    w.CommitMessage.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching workflows by commit message: {ex.Message}");
                throw new GitHubServiceException($"Failed to search workflows by commit message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses a workflow run from a JsonElement
        /// </summary>
        private GitHubWorkflow ParseWorkflowRunFromJson(JsonElement element, GitHubRepoSettings repository)
        {
            try
            {
                var workflow = new GitHubWorkflow
                {
                    RepositoryInfo = repository
                };

                // Parse required fields
                if (element.TryGetProperty("id", out var idElement))
                    workflow.RunId = idElement.GetInt64();

                if (element.TryGetProperty("name", out var nameElement))
                    workflow.Name = nameElement.GetString() ?? "Unknown";

                if (element.TryGetProperty("workflow_id", out var workflowIdElement))
                    workflow.WorkflowId = workflowIdElement.GetInt64();

                if (element.TryGetProperty("run_number", out var runNumberElement))
                    workflow.WorkflowNumber = runNumberElement.GetInt32();

                if (element.TryGetProperty("created_at", out var createdAtElement) &&
                    createdAtElement.TryGetDateTime(out DateTime createdAt))
                    workflow.CreatedAt = createdAt;

                // Parse commit information
                if (element.TryGetProperty("head_sha", out var shaElement))
                    workflow.CommitSha = shaElement.GetString();

                if (element.TryGetProperty("head_commit", out var commitElement))
                {
                    if (commitElement.TryGetProperty("message", out var messageElement))
                        workflow.CommitMessage = messageElement.GetString();
                }

                // Parse PR information if available
                if (element.TryGetProperty("pull_requests", out var prsElement) &&
                    prsElement.GetArrayLength() > 0)
                {
                    var firstPr = prsElement[0];

                    if (firstPr.TryGetProperty("number", out var prNumberElement))
                        workflow.PullRequestNumber = prNumberElement.GetInt32();

                    if (firstPr.TryGetProperty("title", out var prTitleElement))
                        workflow.PullRequestTitle = prTitleElement.GetString();
                }

                // Parse event type
                if (element.TryGetProperty("event", out var eventElement))
                    workflow.EventType = eventElement.GetString() ?? string.Empty;

                return workflow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing workflow run: {ex.Message}");
                return new GitHubWorkflow
                {
                    Name = "Error parsing workflow",
                    RepositoryInfo = repository
                };
            }
        }

        /// <summary>
        /// Converts GitHubWorkflowData to GitHubWorkflow
        /// </summary>
        private GitHubWorkflow ConvertToWorkflow(GitHubWorkflowData workflowData, GitHubRepoSettings repository)
        {
            return new GitHubWorkflow
            {
                RunId = workflowData.Id,
                Name = workflowData.Name,
                WorkflowId = workflowData.WorkflowId,
                WorkflowNumber = workflowData.RunNumber,
                CreatedAt = workflowData.CreatedAt,
                CommitSha = workflowData.HeadSha,
                CommitMessage = workflowData.HeadCommit?.Message,
                PullRequestNumber = workflowData.PullRequests?.FirstOrDefault()?.Number,
                PullRequestTitle = workflowData.PullRequests?.FirstOrDefault()?.Title,
                EventType = workflowData.Event,
                RepositoryInfo = repository
            };
        }

        // Helper class to deserialize the API response
        private class GitHubWorkflowResponse
        {
            [JsonPropertyName("workflow_runs")]
            public List<GitHubWorkflowData> WorkflowRuns { get; set; } = new();
        }

        private class GitHubWorkflowData
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            [JsonPropertyName("workflow_id")]
            public long WorkflowId { get; set; }
            [JsonPropertyName("run_number")]
            public int RunNumber { get; set; }
            [JsonPropertyName("created_at")]
            public DateTime CreatedAt { get; set; }
            [JsonPropertyName("head_sha")]
            public string HeadSha { get; set; } = string.Empty;
            [JsonPropertyName("head_commit")]
            public GitHubCommitData HeadCommit { get; set; } = new();
            [JsonPropertyName("pull_requests")]
            public List<GitHubPrData> PullRequests { get; set; } = new();
            [JsonPropertyName("event")]
            public string Event { get; set; } = string.Empty;
        }

        private class GitHubCommitData
        {
            public string Message { get; set; } = string.Empty;
        }

        private class GitHubPrData
        {
            public int Number { get; set; }
            public string Title { get; set; } = string.Empty;
        }
    }
}
