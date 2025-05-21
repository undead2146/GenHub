using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace GenHub.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Registration module for logging services
    /// </summary>
    public static class LoggingModule
    {
        /// <summary>
        /// Adds logging services to the service collection
        /// </summary>
        public static IServiceCollection AddLoggingModule(this IServiceCollection services, IConfiguration config)
        {
            Console.WriteLine("  - Configuring logging services...");
            
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders(); 

                // Add only basic console and debug logging
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
                
                // Get log level from configuration if available
                var minLogLevel = LogLevel.Information;
                try
                {
                    if (config.GetSection("Logging")?["MinimumLevel"] is string configuredLevel)
                    {
                        if (Enum.TryParse<LogLevel>(configuredLevel, out var parsedLevel))
                        {
                            minLogLevel = parsedLevel;
                            Console.WriteLine($"    - Using configured log level: {minLogLevel}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    - Failed to parse log level from configuration: {ex.Message}");
                }

                loggingBuilder.SetMinimumLevel(minLogLevel);
            });
            
            Console.WriteLine("  - Logging services configured successfully.");
            return services;
        }
        
        /// <summary>
        /// Creates a factory logger that can be used during registration
        /// </summary>
        public static ILoggerFactory CreateBootstrapLoggerFactory()
        {
            return LoggerFactory.Create(builder => {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
        }
    }
}
