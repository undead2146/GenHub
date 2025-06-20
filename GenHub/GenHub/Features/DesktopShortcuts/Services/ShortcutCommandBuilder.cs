using System;
using System.Collections.Generic;
using System.Linq;
using GenHub.Core.Interfaces.DesktopShortcuts;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.DesktopShortcuts.Services
{
    /// <summary>
    /// Service for building command line arguments for desktop shortcuts
    /// </summary>
    public class ShortcutCommandBuilder : IShortcutCommandBuilder
    {
        private readonly ILogger<ShortcutCommandBuilder> _logger;

        public ShortcutCommandBuilder(ILogger<ShortcutCommandBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }        /// <summary>
        /// Builds command line arguments for launching a profile through GenHub
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <returns>Command line arguments string</returns>
        public string BuildCommandLine(ShortcutConfiguration configuration)
        {
            try
            {
                if (configuration == null)
                {
                    throw new ArgumentNullException(nameof(configuration));
                }

                if (string.IsNullOrEmpty(configuration.ProfileId))
                {
                    throw new ArgumentException("Profile ID is required", nameof(configuration));
                }

                var args = new List<string>();

                // Add profile ID as primary argument
                args.Add("--profile");
                args.Add($"\"{configuration.ProfileId}\"");

                // Add launch mode if specified
                if (configuration.LaunchMode != ShortcutLaunchMode.Normal)
                {
                    args.Add("--mode");
                    args.Add(configuration.LaunchMode.ToString().ToLowerInvariant());
                }

                // Add custom arguments if specified
                if (!string.IsNullOrEmpty(configuration.CustomArguments))
                {
                    args.Add("--args");
                    args.Add($"\"{configuration.CustomArguments}\"");
                }

                // Add admin flag if required
                if (configuration.RunAsAdmin)
                {
                    args.Add("--admin");
                }

                // Add quiet mode flag to suppress UI for direct launch
                if (configuration.LaunchMode == ShortcutLaunchMode.Direct)
                {
                    args.Add("--quiet");
                }

                var command = string.Join(" ", args);
                _logger.LogDebug("Built launch command for profile {ProfileId}: {Command}", configuration.ProfileId, command);
                
                return command;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build launch command for profile {ProfileId}", configuration?.ProfileId);
                throw;
            }
        }        /// <summary>
        /// Builds a protocol URL for launching a profile
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <returns>Protocol URL string</returns>
        public string BuildProtocolUrl(ShortcutConfiguration configuration)
        {
            try
            {
                if (configuration == null)
                {
                    throw new ArgumentNullException(nameof(configuration));
                }

                if (string.IsNullOrEmpty(configuration.ProfileId))
                {
                    throw new ArgumentException("Profile ID is required", nameof(configuration));
                }

                var baseUrl = $"genhub://launch/{Uri.EscapeDataString(configuration.ProfileId)}";
                var queryParams = new List<string>();

                // Add launch mode if specified
                if (configuration.LaunchMode != ShortcutLaunchMode.Normal)
                {
                    queryParams.Add($"mode={configuration.LaunchMode.ToString().ToLowerInvariant()}");
                }

                // Add custom arguments if specified
                if (!string.IsNullOrEmpty(configuration.CustomArguments))
                {
                    queryParams.Add($"args={Uri.EscapeDataString(configuration.CustomArguments)}");
                }

                // Add admin flag if required
                if (configuration.RunAsAdmin)
                {
                    queryParams.Add("admin=true");
                }

                var protocolUrl = queryParams.Any() ? $"{baseUrl}?{string.Join("&", queryParams)}" : baseUrl;
                
                _logger.LogDebug("Built protocol URL for profile {ProfileId}: {ProtocolUrl}", configuration.ProfileId, protocolUrl);
                
                return protocolUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build protocol command for profile {ProfileId}", configuration?.ProfileId);
                throw;
            }
        }

        /// <summary>
        /// Validates that a command line is properly formatted
        /// </summary>
        /// <param name="commandLine">Command line to validate</param>
        /// <returns>Validation result</returns>
        public OperationResult ValidateCommandLine(string commandLine)
        {
            try
            {
                if (string.IsNullOrEmpty(commandLine))
                {
                    return OperationResult.Failed("Command line is null or empty");
                }

                // Basic validation - ensure it contains profile argument
                if (!commandLine.Contains("--profile"))
                {
                    return OperationResult.Failed("Command line must contain --profile argument");
                }

                // Validate that quoted arguments are properly closed
                var quoteCount = commandLine.Count(c => c == '"');
                if (quoteCount % 2 != 0)
                {
                    return OperationResult.Failed("Command line contains unmatched quotes");
                }

                _logger.LogDebug("Command line validation passed: {CommandLine}", commandLine);
                return OperationResult.Succeeded("Command line is valid");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating command line: {CommandLine}", commandLine);
                return OperationResult.Failed($"Validation failed: {ex.Message}");
            }
        }
    }
}
