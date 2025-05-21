using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Media.Imaging;

namespace GenHub.Common.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        InitializeWindowIcon();
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
