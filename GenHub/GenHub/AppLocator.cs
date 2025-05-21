using System;
using Microsoft.Extensions.DependencyInjection;

namespace GenHub
{
    /// <summary>
    /// Static service provider access point for the application
    /// </summary>
    public static class AppLocator
    {
        private static IServiceProvider _serviceProvider;

        /// <summary>
        /// Configures the service provider for the application
        /// </summary>
        public static void Configure(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets a service from the service provider
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("AppLocator has not been configured. Call Configure first.");
            }
            
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets a service from the service provider or returns null if not registered
        /// </summary>
        public static T GetServiceOrDefault<T>() where T : class
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("AppLocator has not been configured. Call Configure first.");
            }
            
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets the service provider
        /// </summary>
        public static IServiceProvider Services => _serviceProvider 
            ?? throw new InvalidOperationException("AppLocator has not been configured. Call Configure first.");
    }
}
