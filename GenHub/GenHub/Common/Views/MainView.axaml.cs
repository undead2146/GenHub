using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using GenHub.Common.ViewModels;

namespace GenHub.Common.Views
{
    public partial class MainView : UserControl
    {
        private static ILogger? _logger;

        public MainView()
        {
            InitializeComponent();
            _logger = AppLocator.Services?.GetService<ILogger<MainView>>();
            
            // Ensure DataContext is set
            if (DataContext == null)
            {
                var mainViewModel = AppLocator.Services?.GetService<MainViewModel>();
                if (mainViewModel != null)
                {
                    DataContext = mainViewModel;
                    _logger?.LogInformation("MainView DataContext set to MainViewModel");
                }
                else
                {
                    _logger?.LogError("Could not resolve MainViewModel from services");
                }
            }

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
    }
}
