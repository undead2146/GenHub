using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenHub.Common.ViewModels;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Features.GitHub.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.Downloads.ViewModels;

/// <summary>
/// ViewModel for the Downloads tab.
/// </summary>
public partial class DownloadsViewModel(IServiceProvider serviceProvider, ILogger<DownloadsViewModel> logger) : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Downloads";

    [ObservableProperty]
    private string _description = "Manage your downloads and installations";

    /// <summary>
    /// Performs asynchronous initialization for the Downloads tab.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenGitHubBuilds()
    {
        try
        {
            logger.LogDebug("Opening GitHub manager window from Downloads");

            var gitHubManagerViewModel = serviceProvider.GetRequiredService<GitHubManagerViewModel>();
            var window = new GitHubManagerWindow
            {
                DataContext = gitHubManagerViewModel,
            };
            window.Show();

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open GitHub manager from Downloads");
        }
    }
}
