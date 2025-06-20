using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Microsoft.Extensions.Logging;
using GenHub.Features.GitHub.ViewModels;
using Avalonia.Interactivity;

namespace GenHub.Features.GitHub.Views
{
    public partial class GitHubDetailsView : UserControl
    {
        private readonly ILogger<GitHubDetailsView>? _logger;

        public GitHubDetailsView()
        {
            try
            {
                InitializeComponent();
                _logger = AppLocator.GetServiceOrDefault<ILogger<GitHubDetailsView>>();
                _logger?.LogDebug("GitHubDetailsView initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing GitHubDetailsView: {ex.Message}");
                throw;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            try
            {
                if (DataContext is GitHubDetailsViewModel viewModel)
                {
                    _logger?.LogDebug("GitHubDetailsView loaded with ViewModel");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during GitHubDetailsView loaded event");
            }
        }
    }
}
