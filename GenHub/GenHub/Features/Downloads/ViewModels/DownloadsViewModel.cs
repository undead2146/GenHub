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
using Avalonia.Layout;
using Avalonia.Media;
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
                _logger.LogInformation("Opening GitHub Builds window");

                // Get the GitHub manager view model from DI
                var gitHubManagerViewModel = AppLocator.GetServiceOrDefault<GitHubManagerViewModel>();
                
                if (gitHubManagerViewModel == null)
                {
                    _logger.LogError("Failed to get GitHubManagerViewModel from service provider");
                    return;
                }

                // Create and show the window
                var window = new GitHubManagerWindow(gitHubManagerViewModel);
                
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await window.ShowDialog(desktop.MainWindow);
                }
                else
                {
                    window.Show();
                }

                _logger.LogInformation("GitHub Builds window opened successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening GitHub Builds window: {Message}", ex.Message);
                
                // Simple error notification with proper variable scope
                try
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                    {
                        var dialog = new Window
                        {
                            Title = "Error",
                            Width = 400,
                            Height = 200,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Content = new StackPanel
                            {
                                Margin = new Thickness(20),
                                Spacing = 15,
                                Children =
                                {
                                    new TextBlock { Text = "Failed to open GitHub Manager:", FontWeight = FontWeight.Bold },
                                    new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap },
                                    new Button 
                                    { 
                                        Content = "OK",
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    }
                                }
                            }
                        };
                        
                        // Add click handler after dialog is created
                        if (dialog.Content is StackPanel stackPanel && 
                            stackPanel.Children.Count > 2 && 
                            stackPanel.Children[2] is Button okButton)
                        {
                            okButton.Click += (_, _) => dialog.Close();
                        }
                        
                        await dialog.ShowDialog(desktop.MainWindow);
                    }
                }
                catch (Exception dialogEx)
                {
                    _logger.LogError(dialogEx, "Error showing error dialog");
                }
            }
        }
    }
}
