using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Avalonia;

using GenHub.Core.Interfaces;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Features.GameVersions.Services;
using GenHub.Infrastructure.DependencyInjection;
using GenHub.Windows.UpdateInstallers;
using GenHub.Core.Interfaces.Caching;
using GenHub.Features.AppUpdate.Services;

namespace GenHub.Windows
{
    internal class Program
    {
        private static Mutex? s_Mutex;
        private const string MutexName = "Global\\GenHub";
        private const int SW_RESTORE = 9;
        private static ILogger? _logger;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        public static void Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger<Program>();

            // Ensure single instance logic
            s_Mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running. Find and restore its window.
                Process current = Process.GetCurrentProcess();
                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        IntPtr hWnd = process.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                        }
                        break;
                    }
                }
                return; // Exit if another instance is found
            }
            // Keep the mutex alive for the duration of the application
            GC.KeepAlive(s_Mutex);

            var services = ConfigureServices();
            AppLocator.Configure(services);

            _logger?.LogDebug("Proceeding to BuildAvaloniaApp...");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Configure all needed services for the application
        private static IServiceProvider ConfigureServices()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            var serviceCollection = new ServiceCollection();

            // Register the configuration itself for services that need direct IConfiguration access
            serviceCollection.AddSingleton<IConfiguration>(configuration);
            _logger?.LogDebug("IConfiguration registered as singleton.");

            // Register Windows-specific services
            serviceCollection.AddSingleton<IGameDetector, WindowsGameDetector>();
            _logger?.LogDebug("WindowsGameDetector registered for IGameDetector.");

            // Register WindowsUpdateInstaller as concrete type
            serviceCollection.AddSingleton<WindowsUpdateInstaller>();
            _logger?.LogDebug("WindowsUpdateInstaller registered as concrete type");

            // Register app version service
            serviceCollection.AddSingleton<IAppVersionService, AppVersionService>();

            // Register app update service with version information
            serviceCollection.AddSingleton<IAppUpdateService>(provider => 
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                var versionComparator = provider.GetRequiredService<IVersionComparator>();
                var updateInstaller = provider.GetRequiredService<IUpdateInstaller>();
                var logger = provider.GetRequiredService<ILogger<AppUpdateService>>();
                var versionService = provider.GetRequiredService<IAppVersionService>();
                var cacheService = provider.GetRequiredService<ICacheService>();
                var repositoryManager = provider.GetRequiredService<IGitHubRepositoryManager>();
                
                return new AppUpdateService(
                    httpClient,
                    versionComparator,
                    updateInstaller,
                    versionService,
                    cacheService,
                    logger,
                    repositoryManager
                );
            });

            // Register all common application services 
            serviceCollection.ConfigureApplicationServices(configuration);

            _logger?.LogDebug("About to call BuildServiceProvider...");
            var provider = serviceCollection.BuildServiceProvider();
            _logger?.LogDebug("BuildServiceProvider call SUCCEEDED.");
            _logger?.LogInformation("Done configuring services");
            return provider;
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            _logger?.LogDebug("Building Avalonia app...");
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
            _logger?.LogDebug("AppBuilder created successfully");
            return builder;
        }
    }
}
