using System;
using System.IO;
using Avalonia;
using GenHub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using GenHub.Infrastructure.DependencyInjection;
using GenHub.Core.Interfaces.AppUpdate;
using GenHub.Linux.UpdateInstallers;

namespace GenHub.Linux
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // TODO: Create lockfile to guarantee that only one instance is running on linux

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            // Configure dependency injection
            var services = new ServiceCollection();

            // Register Linux-specific services first
            services.AddSingleton<IGameDetector, LinuxGameDetector>();
            Console.WriteLine("  - LinuxGameDetector registered for IGameDetector.");

            services.AddSingleton<GenHub.Linux.UpdateInstallers.LinuxUpdateInstaller>();

            // Register all common services using our new structured approach
            services.AddAllCommonServices(configuration);

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            AppLocator.Configure(serviceProvider);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
