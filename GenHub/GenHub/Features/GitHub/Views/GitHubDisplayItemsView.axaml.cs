using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using GenHub.Features.GitHub.ViewModels;

namespace GenHub.Features.GitHub.Views
{
    /// <summary>
    /// View for displaying GitHub items tree - pure view with no logic
    /// </summary>
    public partial class GitHubDisplayItemsView : UserControl
    {
        private readonly ILogger<GitHubDisplayItemsView>? _logger;

        public GitHubDisplayItemsView()
        {
            try
            {
                InitializeComponent();
                _logger = AppLocator.GetServiceOrDefault<ILogger<GitHubDisplayItemsView>>();
                _logger?.LogDebug("GitHubDisplayItemsView initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing GitHubDisplayItemsView: {ex.Message}");
                throw;
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            try
            {
                if (DataContext is GitHubItemsTreeViewModel viewModel)
                {
                    _logger?.LogDebug("GitHubDisplayItemsView loaded with ViewModel");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during GitHubDisplayItemsView loaded event");
            }
        }
    }
}
