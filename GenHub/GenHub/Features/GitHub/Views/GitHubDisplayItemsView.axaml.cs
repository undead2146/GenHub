using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GenHub.Features.GitHub.ViewModels;
using Microsoft.Extensions.Logging;
using System;

namespace GenHub.Features.GitHub.Views;

/// <summary>
/// View for displaying GitHub items tree - pure view with no logic.
/// </summary>
public partial class GitHubDisplayItemsView : UserControl
{
    private readonly ILogger<GitHubDisplayItemsView>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubDisplayItemsView"/> class.
    /// </summary>
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

    /// <summary>
    /// Called when the view is loaded.
    /// </summary>
    /// <param name="e">The routed event args.</param>
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

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
