using System;
using System.Collections.Generic;
using System.Linq;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.GitHub.Factories;

/// <summary>
/// Factory for creating display items for GitHub objects.
/// </summary>
public class GitHubDisplayItemFactory(
    IGitHubServiceFacade gitHubServiceFacade,
    ILogger<GitHubDisplayItemFactory> logger)
{
    /// <summary>
    /// Creates display items from GitHub artifacts.
    /// </summary>
    /// <param name="artifacts">The artifacts to create display items from.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <returns>The collection of artifact display item view models.</returns>
    public IEnumerable<GitHubArtifactDisplayItemViewModel> CreateFromArtifacts(IEnumerable<GitHubArtifact> artifacts, string owner, string repository)
    {
        if (artifacts == null)
            return Enumerable.Empty<GitHubArtifactDisplayItemViewModel>();

        return artifacts.Select(artifact => new GitHubArtifactDisplayItemViewModel(
            artifact, gitHubServiceFacade, owner, repository, logger));
    }

    /// <summary>
    /// Creates display items from GitHub releases.
    /// </summary>
    /// <param name="releases">The releases to create display items from.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <returns>The collection of release display item view models.</returns>
    public IEnumerable<GitHubReleaseDisplayItemViewModel> CreateFromReleases(IEnumerable<GitHubRelease> releases, string owner, string repository)
    {
        if (releases == null)
            return Enumerable.Empty<GitHubReleaseDisplayItemViewModel>();

        return releases.Select(release => new GitHubReleaseDisplayItemViewModel(
            release, gitHubServiceFacade, owner, repository, logger));
    }

    /// <summary>
    /// Creates display items from GitHub workflow runs.
    /// </summary>
    /// <param name="workflowRuns">The workflow runs to create display items for.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <returns>An enumerable of workflow display item view models.</returns>
    public IEnumerable<GitHubWorkflowDisplayItemViewModel> CreateFromWorkflowRuns(
        IEnumerable<GitHubWorkflowRun> workflowRuns,
        string owner,
        string repository)
    {
        if (workflowRuns == null)
            return Enumerable.Empty<GitHubWorkflowDisplayItemViewModel>();

        return workflowRuns.Select(workflowRun => new GitHubWorkflowDisplayItemViewModel(
            workflowRun, owner, repository, gitHubServiceFacade, logger));
    }
}
