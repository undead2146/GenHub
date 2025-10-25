using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Core.Constants;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Features.GitHub.Factories;
using GenHub.Features.GitHub.ViewModels.Items;
using GenHub.Features.GitHub.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace GenHub.Features.GitHub.ViewModels;

/// <summary>
/// Main view model for the GitHub manager.
/// </summary>
public partial class GitHubManagerViewModel : ObservableObject
{
    private static GitHubManagerWindow? _currentWindow;

    private readonly IGitHubServiceFacade _gitHubService;
    private readonly GitHubDisplayItemFactory _itemFactory;
    private readonly ILogger<GitHubManagerViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = GitHubConstants.ReadyMessage;

    [ObservableProperty]
    private double _loadingProgress;

    [ObservableProperty]
    private bool _showProgressBar;

    [ObservableProperty]
    private bool _isRepositoryValid;

    [ObservableProperty]
    private ObservableCollection<GitHubDisplayItemViewModel> _items = new();

    [ObservableProperty]
    private GitHubItemsTreeViewModel _itemsTreeViewModel;

    [ObservableProperty]
    private GitHubDetailsViewModel _detailsViewModel;

    [ObservableProperty]
    private InstallationViewModel _installationViewModel;

    [ObservableProperty]
    private RepositoryControlViewModel _repositoryControlVM;

    [ObservableProperty]
    private ContentModeFilterViewModel _contentModeFilterVM;

    private string? _currentOwner;
    private string? _currentRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubManagerViewModel"/> class.
    /// </summary>
    /// <param name="gitHubService">The GitHub service facade.</param>
    /// <param name="itemFactory">The item factory.</param>
    /// <param name="itemsTreeViewModel">The items tree view model.</param>
    /// <param name="detailsViewModel">The details view model.</param>
    /// <param name="installationViewModel">The installation view model.</param>
    /// <param name="repositoryControlVM">The repository control view model.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public GitHubManagerViewModel(
        IGitHubServiceFacade gitHubService,
        GitHubDisplayItemFactory itemFactory,
        GitHubItemsTreeViewModel itemsTreeViewModel,
        GitHubDetailsViewModel detailsViewModel,
        InstallationViewModel installationViewModel,
        RepositoryControlViewModel repositoryControlVM,
        IServiceProvider serviceProvider,
        ILogger<GitHubManagerViewModel> logger)
    {
        _gitHubService = gitHubService ?? throw new ArgumentNullException(nameof(gitHubService));
        _itemFactory = itemFactory ?? throw new ArgumentNullException(nameof(itemFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ItemsTreeViewModel = itemsTreeViewModel ?? throw new ArgumentNullException(nameof(itemsTreeViewModel));
        DetailsViewModel = detailsViewModel ?? throw new ArgumentNullException(nameof(detailsViewModel));
        InstallationViewModel = installationViewModel ?? throw new ArgumentNullException(nameof(installationViewModel));
        RepositoryControlVM = repositoryControlVM ?? throw new ArgumentNullException(nameof(repositoryControlVM));
        ContentModeFilterVM = new ContentModeFilterViewModel();

        // Wire up the tree view selection to update details and installation
        ItemsTreeViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GitHubItemsTreeViewModel.SelectedItem))
            {
                var selectedGitHubItem = ItemsTreeViewModel.SelectedItem?.Child;
                DetailsViewModel.SetSelectedItem(selectedGitHubItem);
                InstallationViewModel.SetSelectedItem(selectedGitHubItem);

                // Log for debugging
                if (selectedGitHubItem != null)
                {
                    _logger.LogDebug(
                        "Selected item: {DisplayName}, CanInstall: {CanInstall}",
                        selectedGitHubItem.DisplayName,
                        selectedGitHubItem.CanInstall);
                }
            }
        };

        // Also wire up the SelectedGitHubItemChanged event for direct updates
        ItemsTreeViewModel.SelectedGitHubItemChanged += (s, selectedItem) =>
        {
            DetailsViewModel.SetSelectedItem(selectedItem);
            InstallationViewModel.SetSelectedItem(selectedItem);

            if (selectedItem != null)
            {
                _logger.LogDebug(
                    "Direct selection: {DisplayName}, Type: {Type}",
                    selectedItem.DisplayName,
                    selectedItem.GetType().Name);
            }
        };

        // Wire up repository selection change
        RepositoryControlVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RepositoryControlVM.SelectedRepository))
            {
                LoadItemsForSelectedRepository();
            }
        };

        // Auto-discover repositories on initialization
        _ = Task.Run(() =>
        {
            try
            {
                if (RepositoryControlVM?.DiscoverRepositoriesCommand?.CanExecute(null) == true)
                {
                    RepositoryControlVM.DiscoverRepositoriesCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-discovery failed on initialization");
            }
        });
    }

    /// <summary>
    /// Gets the current owner.
    /// </summary>
    public string? CurrentOwner => _currentOwner;

    /// <summary>
    /// Gets the current repository.
    /// </summary>
    public string? CurrentRepository => _currentRepository;

    private bool CanLoadRepository => !IsLoading && RepositoryControlVM?.SelectedRepository != null;

    /// <summary>
    /// Opens the GitHub manager window (singleton pattern).
    /// </summary>
    [RelayCommand]
    public static void OpenGitHubManager()
    {
        if (_currentWindow != null)
        {
            _currentWindow.Activate();
            return;
        }

        _currentWindow = new GitHubManagerWindow();
        _currentWindow.Show();
        _currentWindow.Closed += (s, e) => _currentWindow = null;
    }

    /// <summary>
    /// Loads the repository.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadRepository))]
    private async Task LoadRepositoryAsync()
    {
        if (!CanLoadRepository) return;

        IsLoading = true;
        ShowProgressBar = true;
        LoadingProgress = 0;
        StatusMessage = "Initializing repository data...";
        await Task.Delay(100); // Brief delay to show start

        try
        {
            Items.Clear();
            ItemsTreeViewModel.SetItems(Items, null, null); // Clear tree immediately
            LoadingProgress = 10;

            var repo = RepositoryControlVM.SelectedRepository;
            if (repo == null) return;

            _currentOwner = repo.RepoOwner;
            _currentRepository = repo.RepoName;
            LoadingProgress = 20;

            // Load releases
            StatusMessage = $"Loading releases for {repo.RepoOwner}/{repo.RepoName}...";
            LoadingProgress = 25;

            var releasesResult = await _gitHubService.GetReleasesAsync(repo.RepoOwner, repo.RepoName);
            LoadingProgress = 45;

            if (releasesResult.Success && releasesResult.Data?.Any() == true)
            {
                StatusMessage = $"Processing {releasesResult.Data.Count()} releases...";
                var releaseItems = _itemFactory.CreateFromReleases(releasesResult.Data, repo.RepoOwner, repo.RepoName);
                foreach (var item in releaseItems)
                {
                    Items.Add(item);
                }
            }

            LoadingProgress = 50;

            // Load workflow runs
            StatusMessage = $"Loading workflows for {repo.RepoOwner}/{repo.RepoName}...";
            LoadingProgress = 55;

            var workflowsResult = await _gitHubService.GetWorkflowRunsForRepositoryAsync(repo.RepoOwner, repo.RepoName);
            LoadingProgress = 75;

            if (workflowsResult.Success && workflowsResult.Data?.Any() == true)
            {
                StatusMessage = $"Processing {workflowsResult.Data.Count()} workflows...";
                var workflowItems = _itemFactory.CreateFromWorkflowRuns(workflowsResult.Data, repo.RepoOwner, repo.RepoName);
                foreach (var item in workflowItems)
                {
                    Items.Add(item);
                }
            }

            LoadingProgress = 85;

            // Update the tree view with the loaded items
            StatusMessage = "Organizing items in tree view...";
            LoadingProgress = 90;
            ItemsTreeViewModel.SetItems(Items, _currentOwner, _currentRepository);

            LoadingProgress = 100;
            var releaseCount = releasesResult.Data?.Count() ?? 0;
            var workflowCount = workflowsResult.Data?.Count() ?? 0;

            // Build status message based on what was actually loaded
            var statusParts = new List<string>();
            if (releaseCount > 0) statusParts.Add($"{releaseCount} releases");
            if (workflowCount > 0) statusParts.Add($"{workflowCount} workflows");

            var itemsDescription = statusParts.Any() ? $" ({string.Join(", ", statusParts)})" : string.Empty;
            StatusMessage = $"✅ Loaded {Items.Count} items from {repo.RepoOwner}/{repo.RepoName}{itemsDescription}";
            IsRepositoryValid = true;

            _logger.LogInformation(
                "Successfully loaded repository {Owner}/{Repo}: {ReleaseCount} releases, {WorkflowCount} workflows",
                _currentOwner,
                _currentRepository,
                releaseCount,
                workflowCount);

            // Show completion for a moment before hiding progress bar
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load repository {Owner}/{Name}", _currentOwner, _currentRepository);
            StatusMessage = $"❌ Error loading {_currentOwner}/{_currentRepository}: {ex.Message}";
            IsRepositoryValid = false;
        }
        finally
        {
            IsLoading = false;
            ShowProgressBar = false;
            LoadingProgress = 0;
        }
    }

    private void LoadItemsForSelectedRepository()
    {
        if (RepositoryControlVM.SelectedRepository != null)
        {
            _ = LoadRepositoryAsync();
        }
    }

    /// <summary>
    /// Opens the GitHub token dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenGitHubTokenDialogAsync()
    {
        try
        {
            var tokenDialogViewModel = _serviceProvider.GetRequiredService<GitHubTokenDialogViewModel>();
            tokenDialogViewModel.Reset(); // Reset state before showing

            var dialog = new GitHubTokenDialogView
            {
                DataContext = tokenDialogViewModel,
            };

            // Subscribe to the ShouldClose property to handle dialog closing
            tokenDialogViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GitHubTokenDialogViewModel.ShouldClose) &&
                    tokenDialogViewModel.ShouldClose)
                {
                    dialog.Close(tokenDialogViewModel.DialogResult);
                }
            };

            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var parentWindow = _currentWindow ?? lifetime?.MainWindow;

            bool? result = false;
            if (parentWindow != null)
            {
                result = await dialog.ShowDialog<bool?>(parentWindow);
            }
            else
            {
                dialog.Show();
            }

            // Handle the result and bring focus back to GitHub Manager
            if (result == true)
            {
                StatusMessage = "GitHub token configured successfully";
                _logger.LogInformation("GitHub token configured successfully");

                // Auto-refresh repositories after token configuration
                if (RepositoryControlVM?.DiscoverRepositoriesCommand?.CanExecute(null) == true)
                {
                    RepositoryControlVM.DiscoverRepositoriesCommand.Execute(null);
                }
            }
            else
            {
                StatusMessage = "GitHub token configuration cancelled";
            }

            // Bring focus back to the GitHub Manager window
            _currentWindow?.Activate();
            _currentWindow?.Focus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open GitHub token dialog");
            StatusMessage = $"Error opening token dialog: {ex.Message}";
        }
    }
}
