using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using GenHub.Common.ViewModels;
using Microsoft.Extensions.Logging;

namespace GenHub.Common.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        InitializeWindowIcon();

        var logger = AppLocator.Services?.GetService<ILogger<MainWindow>>();

        // Ensure MainView gets the MainViewModel
        var mainViewModel = AppLocator.Services?.GetService<MainViewModel>();
        if (mainViewModel != null)
        {
            // Find the MainView and set its DataContext
            var mainView = this.FindControl<MainView>("MainViewRoot") ?? this.Content as MainView;
            if (mainView != null)
            {
                mainView.DataContext = mainViewModel;
                logger?.LogInformation("MainWindow set MainView DataContext to MainViewModel");
            }
            else
            {
                logger?.LogWarning("Could not find MainView in MainWindow");
            }
        }
        else
        {
            logger?.LogError("Could not resolve MainViewModel for MainWindow");
        }
    }
    
    private void InitializeWindowIcon()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var iconUri = new Uri("avares://GenHub/Assets/Icons/genhub-logo.png");

            using var assetStream = AssetLoader.Open(iconUri);
            if (assetStream == null)
            {
                Console.WriteLine("Failed to load icon: stream is null");
                return;
            }

            using var bitmap = new Bitmap(assetStream);
            Icon = new WindowIcon(bitmap);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Window icon error: {ex.GetType().Name}: {ex.Message}");
            // More detailed logging for debugging
            if (ex.InnerException != null)
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
        }
    }
}
