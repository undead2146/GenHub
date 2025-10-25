using System;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.ViewModels.Items;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// View model for GitHub items tree.
/// </summary>
public partial class GitHubItemsTreeViewModel : INotifyPropertyChanged
{
    private readonly IGitHubServiceFacade _gitHubService;
    private readonly ILogger<GitHubItemsTreeViewModel> _logger;
    private readonly RepositoryControlViewModel _repositoryControl;
    private TreeNode? _selectedItem;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubItemsTreeViewModel"/> class.
    /// </summary>
    /// <param name="gitHubService">The GitHub service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="repositoryControl">The repository control view model.</param>
    public GitHubItemsTreeViewModel(IGitHubServiceFacade gitHubService, ILogger<GitHubItemsTreeViewModel> logger, RepositoryControlViewModel repositoryControl)
    {
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repositoryControl = repositoryControl ?? throw new ArgumentNullException(nameof(repositoryControl));
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when the selected GitHub item changes.
    /// </summary>
    public event EventHandler<GitHubDisplayItemViewModel?>? SelectedGitHubItemChanged;

    /// <summary>
    /// Gets the items collection.
    /// </summary>
    public ObservableCollection<TreeNode> Items { get; } = new();

    /// <summary>
    /// Gets or sets the selected item.
    /// </summary>
    public TreeNode? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                _selectedItem = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
                SelectedGitHubItemChanged?.Invoke(this, value?.Child);
            }
        }
    }

    /// <summary>
    /// Expands all tree nodes.
    /// </summary>
    [RelayCommand]
    public void ExpandAll()
    {
        _logger.LogInformation("ExpandAll command executed. Processing {Count} top-level items", Items.Count);
        ExpandAllNodes(Items);
        _logger.LogInformation("ExpandAll command completed");
    }

    /// <summary>
    /// Collapses all tree nodes.
    /// </summary>
    [RelayCommand]
    public void CollapseAll()
    {
        _logger.LogInformation("CollapseAll command executed. Processing {Count} top-level items", Items.Count);
        CollapseAllNodes(Items);
        _logger.LogInformation("CollapseAll command completed");
    }

    /// <summary>
    /// Sets the items in the tree.
    /// </summary>
    /// <param name="items">The flat list of items.</param>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repository">The repository name.</param>
    public void SetItems(ObservableCollection<GitHubDisplayItemViewModel> items, string? owner = null, string? repository = null)
    {
        this.Items.Clear();

        if (items == null)
        {
            return;
        }

        // Group by type for hierarchy
        var releases = items.Where(i => i.IsRelease).ToList();
        var workflows = items.Where(i => i.IsWorkflowRun).ToList();

        if (releases.Any())
        {
            var releaseFolder = new TreeNode
            {
                DisplayName = GitHubConstants.ReleasesFolderName,
                IsFolder = true,
            };

            foreach (var releaseItem in releases)
            {
                var releaseNode = new TreeNode
                {
                    DisplayName = releaseItem.DisplayName,
                    Description = releaseItem.Description,
                    Child = releaseItem,
                    IsFolder = releaseItem.IsExpandable,
                };

                // For releases, we need to populate their asset children immediately
                if (releaseItem.IsExpandable && releaseItem is GitHubReleaseDisplayItemViewModel releaseVm)
                {
                    // Create asset nodes from the release assets directly
                    if (releaseVm.Release.Assets != null)
                    {
                        foreach (var asset in releaseVm.Release.Assets)
                        {
                            // Create a simple artifact item for display in the tree
                            var artifact = new GitHubArtifact
                            {
                                Name = asset.Name,
                                SizeInBytes = asset.Size,
                                DownloadUrl = asset.BrowserDownloadUrl,
                                CreatedAt = asset.CreatedAt.DateTime,
                                IsInstalled = false,
                            };

                            // Use provided owner and repository, or fall back to repository control
                            var effectiveOwner = owner ?? _repositoryControl.SelectedRepository?.RepoOwner ?? "unknown";
                            var effectiveRepository = repository ?? _repositoryControl.SelectedRepository?.RepoName ?? "unknown";

                            var artifactVm = new GitHubArtifactDisplayItemViewModel(
                                artifact,
                                _gitHubService,
                                effectiveOwner,
                                effectiveRepository,
                                _logger);

                            releaseNode.Children.Add(new TreeNode
                            {
                                DisplayName = artifactVm.DisplayName,
                                Description = artifactVm.Description,
                                Child = artifactVm,
                                IsFolder = false,
                            });
                        }
                    }
                }

                releaseFolder.Children.Add(releaseNode);
            }

            this.Items.Add(releaseFolder);
        }

        if (workflows.Any())
        {
            var workflowFolder = new TreeNode
            {
                DisplayName = GitHubConstants.WorkflowBuildsFolderName,
                IsFolder = true,
            };
            foreach (var item in workflows)
            {
                workflowFolder.Children.Add(new TreeNode
                {
                    DisplayName = item.DisplayName,
                    Description = item.Description,
                    Child = item,
                    IsFolder = false,
                });
            }

            this.Items.Add(workflowFolder);
        }

        // Add flat items if no grouping
        if (!releases.Any() && !workflows.Any())
        {
            foreach (var item in items)
            {
                this.Items.Add(new TreeNode
                {
                    DisplayName = item.DisplayName,
                    Description = item.Description,
                    Child = item,
                    IsFolder = false,
                });
            }
        }
    }

    /// <summary>
    /// Clears the items.
    /// </summary>
    public void ClearItems()
    {
        this.Items.Clear();
        SelectedItem = null;
    }

    /// <summary>
    /// Loads items for the current repository.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public async Task LoadItemsForRepository()
    {
        // Implementation handled in parent ViewModel
        await Task.CompletedTask;
    }

    private void ExpandAllNodes(ObservableCollection<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            // Expand all folder nodes and nodes with children
            if (node.IsFolder || node.Children.Any())
            {
                node.IsExpanded = true;

                // Load children for expandable items that haven't been loaded yet
                if (node.Child?.IsExpandable == true && !node.Child.Children.Any())
                {
                    _ = node.Child.LoadChildrenAsync();
                }
            }

            // Recursively expand children
            if (node.Children.Any())
            {
                ExpandAllNodes(node.Children);
            }
        }

        // Force UI update
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
    }

    private void CollapseAllNodes(ObservableCollection<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            // Collapse all expanded nodes
            if (node.IsExpanded)
            {
                node.IsExpanded = false;
            }

            // Recursively collapse children
            if (node.Children.Any())
            {
                CollapseAllNodes(node.Children);
            }
        }

        // Force UI update
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
    }
}
