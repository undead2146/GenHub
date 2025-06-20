using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using GenHub.Features.GitHub.ViewModels;
using System;

namespace GenHub.Features.GitHub.Views
{
    /// <summary>
    /// View for installation operations
    /// </summary>
    public partial class InstallationView : UserControl
    {
        private readonly ILogger<InstallationView>? _logger;

        public InstallationView()
        {
            try
            {
                InitializeComponent();
                _logger = AppLocator.GetServiceOrDefault<ILogger<InstallationView>>();
                _logger?.LogDebug("InstallationView initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing InstallationView: {ex.Message}");
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            try
            {
                if (DataContext is InstallationViewModel viewModel)
                {
                    _logger?.LogDebug("InstallationView loaded with ViewModel");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during InstallationView loaded event");
            }
        }
    }
}
