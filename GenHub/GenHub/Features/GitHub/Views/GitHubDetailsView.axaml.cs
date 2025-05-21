using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Microsoft.Extensions.Logging;
using GenHub.Features.GitHub.ViewModels;

namespace GenHub.Features.GitHub.Views
{
    public partial class GitHubDetailsView : UserControl
    {
        private readonly ILogger<GitHubDetailsView>? _logger;

        public GitHubDetailsView()
        {
            try
            {
                // Attempt to get logger from service provider if available
                _logger = AppLocator.GetService<ILogger<GitHubDetailsView>>();
                
                InitializeComponent();
                DataContextChanged += OnDataContextChanged;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing GitHubDetailsView: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is GitHubDetailsViewModel viewModel)
            {
                _logger?.LogDebug("GitHubDetailsView received ViewModel: {ItemName}", 
                    viewModel.SelectedGitHubItem?.DisplayName ?? "null");
            }
        }
    }
}
