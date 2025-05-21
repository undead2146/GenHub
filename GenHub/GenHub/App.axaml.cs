using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GenHub.Common.ViewModels; // Assuming MainViewModel is here
using GenHub.Common.Views;    // Assuming MainWindow is here
using Microsoft.Extensions.DependencyInjection;
using System; // For Console.WriteLine
using System.Threading.Tasks; // For Task.Run
using Avalonia.Threading; // For Dispatcher

namespace GenHub
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            Console.WriteLine("[App.Initialize] Starting.");
            AvaloniaXamlLoader.Load(this);
            Console.WriteLine("[App.Initialize] AvaloniaXamlLoader.Load completed.");
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Console.WriteLine("[App.OnFrameworkInitializationCompleted] Starting.");
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.WriteLine("[App.OnFrameworkInitializationCompleted] ApplicationLifetime is IClassicDesktopStyleApplicationLifetime.");


                Console.WriteLine("[App.OnFrameworkInitializationCompleted] Attempting to create and initialize MainViewModel asynchronously.");
                _ = Task.Run(async () => {
                    try
                    {
                        Console.WriteLine("[App.OnFrameworkInitializationCompleted - Task] Resolving MainViewModel...");
                        var mainViewModel = AppLocator.Services.GetRequiredService<MainViewModel>();
                        Console.WriteLine("[App.OnFrameworkInitializationCompleted - Task] MainViewModel resolved.");

                        Console.WriteLine("[App.OnFrameworkInitializationCompleted - Task] Calling MainViewModel.InitializeAsync (if exists)...");
                        if (mainViewModel is IAsyncInitializable vmAsyncInit) 
                        {
                            await vmAsyncInit.InitializeAsync();
                        }
                        Console.WriteLine("[App.OnFrameworkInitializationCompleted - Task] MainViewModel.InitializeAsync completed.");

                        Console.WriteLine("[App.OnFrameworkInitializationCompleted - Task] Dispatching to UI thread to set DataContext.");
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Console.WriteLine("[App.OnFrameworkInitializationCompleted - UIThread] Setting MainWindow DataContext.");
                            if (desktop.MainWindow == null) 
                            {
                                desktop.MainWindow = new MainWindow();
                            }
                            desktop.MainWindow.DataContext = mainViewModel;
                            Console.WriteLine("[App.OnFrameworkInitializationCompleted - UIThread] MainWindow DataContext set.");
                            if (!desktop.MainWindow.IsVisible)
                            {
                                desktop.MainWindow.Show();
                                Console.WriteLine("[App.OnFrameworkInitializationCompleted - UIThread] MainWindow shown.");
                            }
                        });
                        Console.WriteLine("[App.OnFrameworkInitializationCompleted - Task] UI thread dispatch completed.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[App.OnFrameworkInitializationCompleted - FATAL ERROR IN TASK] {ex}");
                        // Optionally show error on UI
                        await Dispatcher.UIThread.InvokeAsync(() => {
                             if (desktop.MainWindow == null) desktop.MainWindow = new MainWindow();
                             desktop.MainWindow.Content = new Avalonia.Controls.TextBlock { Text = "Application failed to start. Check console. " + ex.Message, Foreground = Avalonia.Media.Brushes.Red, Margin=new Thickness(20) };
                             if (!desktop.MainWindow.IsVisible) desktop.MainWindow.Show();
                        });
                    }
                });
            }
            else
            {
                Console.WriteLine("[App.OnFrameworkInitializationCompleted] ApplicationLifetime is NOT IClassicDesktopStyleApplicationLifetime.");
            }
            base.OnFrameworkInitializationCompleted();
            Console.WriteLine("[App.OnFrameworkInitializationCompleted] Base call completed. Method exiting.");
        }
    }

    // Define this interface somewhere accessible, e.g., in Common or Core
    public interface IAsyncInitializable
    {
        Task InitializeAsync();
    }
}
