using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenHub.Features.GitHub.ViewModels.Items;

/// <summary>
/// View model for a GitHub workflow run.
/// </summary>
public partial class GitHubWorkflowDisplayItemViewModel : GitHubDisplayItemViewModel
{
    private readonly IGitHubServiceFacade _gitHubService;
    private readonly string _owner;
    private readonly string _repository;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoadingChildren;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubWorkflowDisplayItemViewModel"/> class.
    /// </summary>
    /// <param name="workflowRun">The GitHub workflow run to display.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="gitHubService">The GitHub service facade.</param>
    /// <param name="logger">The logger instance.</param>
    public GitHubWorkflowDisplayItemViewModel(
        GitHubWorkflowRun workflowRun,
        string owner,
        string repository,
        IGitHubServiceFacade gitHubService,
        ILogger logger)
        : base(logger)
    {
        WorkflowRun = workflowRun ?? throw new ArgumentNullException(nameof(workflowRun));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
    }

    /// <summary>
    /// Gets the underlying GitHub workflow run.
    /// </summary>
    public GitHubWorkflowRun WorkflowRun { get; }

    /// <summary>
    /// Gets the display name for the workflow run.
    /// </summary>
    public override string DisplayName => WorkflowRun.Workflow?.Name ?? $"Workflow Run #{WorkflowRun.RunNumber}";

    /// <summary>
    /// Gets the description for the workflow run.
    /// </summary>
    public override string Description => $"{WorkflowRun.Status} - {WorkflowRun.Conclusion} - Started {WorkflowRun.CreatedAt:g}";

    /// <summary>
    /// Gets a value indicating whether the workflow run is expandable.
    /// </summary>
    public override bool IsExpandable => true;

    /// <summary>
    /// Gets the sort date for the workflow run.
    /// </summary>
    public override DateTime SortDate => WorkflowRun.CreatedAt.DateTime;

    /// <summary>
    /// Gets a value indicating whether this is a release item.
    /// </summary>
    public override bool IsRelease => false;

    /// <summary>
    /// Gets a value indicating whether the workflow run can be downloaded.
    /// </summary>
    public override bool CanDownload => false; // Workflows don't download directly

    /// <summary>
    /// Gets a value indicating whether the workflow run can be installed.
    /// </summary>
    public override bool CanInstall => false; // Workflows don't install directly

    /// <summary>
    /// Gets a value indicating whether this is a workflow run item.
    /// </summary>
    public override bool IsWorkflowRun => true;

    /// <summary>
    /// Toggles the expanded state of the workflow run view model.
    /// </summary>
    /// <returns>The task representing the operation.</returns>
    public override async Task ToggleExpandedAsync()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            Children.Clear();
        }
        else
        {
            await LoadChildrenAsync();
        }
    }

    /// <summary>
    /// Loads the child artifacts for the workflow run asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task representing the operation.</returns>
    public override async Task LoadChildrenAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoadingChildren || Children.Any()) return;

        IsLoadingChildren = true;
        try
        {
            // Get artifacts for this workflow run
            var artifactsResult = await _gitHubService.GetArtifactsForWorkflowRunAsync(
                _owner,
                _repository,
                WorkflowRun.Id,
                cancellationToken);

            if (artifactsResult.Success && artifactsResult.Data != null)
            {
                foreach (var artifact in artifactsResult.Data)
                {
                    var artifactVm = new GitHubArtifactDisplayItemViewModel(
                        artifact,
                        _gitHubService,
                        _owner,
                        _repository,
                        Logger);

                    Children.Add(artifactVm);
                }
            }

            IsExpanded = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load workflow artifacts for run {RunNumber}", WorkflowRun.RunNumber);
        }
        finally
        {
            IsLoadingChildren = false;
        }
    }
}
