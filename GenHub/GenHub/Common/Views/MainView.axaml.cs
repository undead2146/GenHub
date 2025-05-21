using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Avalonia.Controls.ApplicationLifetimes;

using GenHub.Features.GitHub.ViewModels;
using GenHub.Features.GitHub.Views;
using GenHub.Common.ViewModels;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Interfaces;
using GenHub.Features.GitHub.Services;

namespace GenHub.Common.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();

            this.Loaded += async (s, e) =>
            {
                if (DataContext is MainViewModel viewModel)
                {
                    try
                    {
                        await viewModel.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in InitializeAsync: {ex.Message}");
                    }
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        /// <summary>
        /// Static helper to open the GitHub Builds window
        /// </summary>
        public static async void OpenGitHubBuildsWindow()
        {
            try
            {
                var gitHubWindow = new GitHubManagerWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Title = "GitHub Builds Manager"
                };

                // Set the DataContext if needed
                var vm = AppLocator.GetService<GitHubManagerViewModel>();

                // Show the window modally if this is being called from a window
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window parentWindow)
                {
                    await gitHubWindow.ShowDialog(parentWindow);
                }
                else
                {
                    gitHubWindow.Show();
                }
            }

            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"Error opening GitHub window: {ex.Message}");
            }
        }
    }
}
