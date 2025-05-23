using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

using GenHub.Common.ViewModels;
using GenHub.Features.GitHub.ViewModels;
using GenHub.Features.GitHub.Views;

namespace GenHub.Features.Downloads.ViewModels
{
    /// <summary>
    /// ViewModel for the Downloads tab following MVVM architecture
    /// </summary>
    public partial class DownloadsViewModel : ViewModelBase
    {
        private readonly ILogger<DownloadsViewModel> _logger;

        [ObservableProperty]
        private string _title = "Downloads";

        [ObservableProperty]
        private string _description = "Manage your downloads and installations";

        public DownloadsViewModel(ILogger<DownloadsViewModel> logger)
        {
            _logger = logger;
            _logger.LogDebug("DownloadsViewModel initialized");
        }

        [RelayCommand]
        private async Task OpenGitHubBuilds()
        {
            try
            {
                _logger.LogInformation("Opening GitHub Builds window from Downloads tab");
                
                // Get the GitHubManagerViewModel from DI container
                var gitHubViewModel = AppLocator.Services?.GetService<GitHubManagerViewModel>();
                if (gitHubViewModel == null)
                {
                    _logger.LogError("Could not resolve GitHubManagerViewModel from service container");
                    return;
                }

                var gitHubWindow = new GitHubManagerWindow
                {
                    DataContext = gitHubViewModel,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Title = "GitHub Builds Manager"
                };

                // Show the window modally if possible
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && 
                    desktop.MainWindow is Window parentWindow)
                {
                    await gitHubWindow.ShowDialog(parentWindow);
                }
                else
                {
                    gitHubWindow.Show();
                }
                
                _logger.LogInformation("GitHub Builds window opened successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening GitHub Builds window: {Message}", ex.Message);
            }
        }
    }
}
