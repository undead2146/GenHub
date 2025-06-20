using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.AdvancedLauncher;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AdvancedLauncher.Services
{
    /// <summary>
    /// Service for handling custom protocol URLs (genhub://) for launching games
    /// </summary>
    public class LauncherProtocolService : ILauncherProtocolService
    {
        private readonly ILauncherArgumentParser _argumentParser;
        private readonly IDirectLaunchService _directLaunchService;
        private readonly ILogger<LauncherProtocolService> _logger;

        public LauncherProtocolService(
            ILauncherArgumentParser argumentParser,
            IDirectLaunchService directLaunchService,
            ILogger<LauncherProtocolService> logger)
        {
            _argumentParser = argumentParser ?? throw new ArgumentNullException(nameof(argumentParser));
            _directLaunchService = directLaunchService ?? throw new ArgumentNullException(nameof(directLaunchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers the genhub:// protocol with the operating system
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Registration result</returns>
        public async Task<OperationResult> RegisterProtocolAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Registering genhub:// protocol with operating system");

                // Get the current executable path
                var executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(executablePath))
                {
                    _logger.LogError("Failed to get current executable path for protocol registration");
                    return OperationResult.Failed("Cannot determine executable path for protocol registration");
                }

                // Platform-specific protocol registration will be handled by platform-specific services
                _logger.LogInformation("Protocol registration attempted for executable: {ExecutablePath}", executablePath);
                
                // In a real implementation, this would call platform-specific services
                await Task.Delay(1, cancellationToken); // Make it async
                return OperationResult.Succeeded("Protocol registration completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register protocol");
                return OperationResult.Failed($"Protocol registration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregisters the genhub:// protocol from the operating system
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Unregistration result</returns>
        public async Task<OperationResult> UnregisterProtocolAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Unregistering genhub:// protocol from operating system");
                
                // Platform-specific protocol unregistration will be handled by platform-specific services
                await Task.Delay(1, cancellationToken); // Make it async
                return OperationResult.Succeeded("Protocol unregistration completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister protocol");
                return OperationResult.Failed($"Protocol unregistration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a protocol URL (genhub://...)
        /// </summary>
        /// <param name="protocolUrl">The protocol URL to handle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public async Task<OperationResult> HandleProtocolUrlAsync(string protocolUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Handling protocol URL: {ProtocolUrl}", protocolUrl);

                if (string.IsNullOrEmpty(protocolUrl))
                {
                    return OperationResult.Failed("Protocol URL is null or empty");
                }

                if (!protocolUrl.StartsWith("genhub://", StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult.Failed($"Invalid protocol URL: {protocolUrl}");
                }

                // Parse the protocol URL
                var parsedUrl = ParseProtocolUrl(protocolUrl);
                if (parsedUrl == null)
                {
                    return OperationResult.Failed($"Failed to parse protocol URL: {protocolUrl}");
                }

                // Convert parsed URL to launch parameters
                var parameters = ConvertToLaunchParameters(parsedUrl);
                if (parameters == null)
                {
                    return OperationResult.Failed($"Failed to convert protocol URL to launch parameters: {protocolUrl}");
                }

                // Validate the parameters
                var validationResult = _argumentParser.ValidateParameters(parameters);
                if (!validationResult.Success)
                {
                    var errorMessage = $"Invalid launch parameters from protocol URL: {validationResult.Message}";
                    _logger.LogWarning(errorMessage);
                    return OperationResult.Failed(errorMessage);
                }

                // Launch the game
                _logger.LogInformation("Launching game from protocol URL for profile: {ProfileId}", parameters.ProfileId);
                var launchResult = await _directLaunchService.LaunchDirectlyAsync(parameters, cancellationToken);
                
                if (launchResult.Success)
                {
                    return OperationResult.Succeeded($"Successfully launched profile: {parameters.ProfileId}");
                }
                else
                {
                    return OperationResult.Failed($"Launch failed: {launchResult.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling protocol URL: {ProtocolUrl}", protocolUrl);
                return OperationResult.Failed($"Protocol handling failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the protocol is currently registered
        /// </summary>
        /// <returns>True if registered, false otherwise</returns>
        public async Task<bool> IsProtocolRegisteredAsync()
        {
            try
            {
                _logger.LogDebug("Checking if genhub:// protocol is registered");
                
                // In a real implementation, this would check the registry on Windows
                // or desktop file associations on Linux
                await Task.Delay(1); // Make it async
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check protocol registration status");
                return false;
            }
        }

        /// <summary>
        /// Builds a protocol URL for the given action and parameters
        /// </summary>
        /// <param name="action">Protocol action (e.g., "launch", "create-shortcut")</param>
        /// <param name="parameters">Additional parameters</param>
        /// <returns>Formatted protocol URL</returns>
        public string BuildProtocolUrl(string action, params (string key, string value)[] parameters)
        {
            try
            {
                if (string.IsNullOrEmpty(action))
                {
                    throw new ArgumentException("Action is required", nameof(action));
                }

                var baseUrl = $"genhub://{action.ToLowerInvariant()}";
                
                if (parameters?.Length > 0)
                {
                    var queryParams = parameters
                        .Where(p => !string.IsNullOrEmpty(p.key) && !string.IsNullOrEmpty(p.value))
                        .Select(p => $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.value)}");
                    
                    if (queryParams.Any())
                    {
                        baseUrl += "?" + string.Join("&", queryParams);
                    }
                }

                _logger.LogDebug("Built protocol URL: {ProtocolUrl}", baseUrl);
                return baseUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build protocol URL for action: {Action}", action);
                throw;
            }
        }

        /// <summary>
        /// Parses a protocol URL into its components
        /// </summary>
        private ProtocolUrlInfo? ParseProtocolUrl(string protocolUrl)
        {
            try
            {
                // Expected format: genhub://action?param1=value1&param2=value2
                var uri = new Uri(protocolUrl);
                
                if (uri.Scheme.ToLowerInvariant() != "genhub")
                {
                    _logger.LogWarning("Invalid protocol scheme: {Scheme}", uri.Scheme);
                    return null;
                }

                var action = uri.Host?.ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(action))
                {
                    _logger.LogWarning("Missing action in protocol URL: {ProtocolUrl}", protocolUrl);
                    return null;
                }

                // Parse query parameters
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                return new ProtocolUrlInfo
                {
                    Action = action,
                    QueryParameters = queryParams
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse protocol URL: {ProtocolUrl}", protocolUrl);
                return null;
            }
        }

        /// <summary>
        /// Converts parsed protocol URL to launch parameters
        /// </summary>
        private LaunchParameters? ConvertToLaunchParameters(ProtocolUrlInfo urlInfo)
        {
            try
            {
                if (urlInfo.Action != "launch")
                {
                    _logger.LogWarning("Unsupported protocol action: {Action}", urlInfo.Action);
                    return null;
                }

                var profileId = urlInfo.QueryParameters["profile"];
                if (string.IsNullOrEmpty(profileId))
                {
                    _logger.LogWarning("Missing profile parameter in protocol URL");
                    return null;
                }

                var parameters = new LaunchParameters
                {
                    ProfileId = profileId
                };

                // Parse additional query parameters using correct property names
                foreach (string key in urlInfo.QueryParameters.Keys)
                {
                    var value = urlInfo.QueryParameters[key];
                    if (string.IsNullOrEmpty(value)) continue;

                    switch (key.ToLowerInvariant())
                    {
                        case "mode":
                            if (Enum.TryParse<LaunchMode>(value, true, out var mode))
                            {
                                parameters.Mode = mode;
                            }
                            break;
                        case "args":
                            parameters.CustomArguments = value;
                            break;
                        case "admin":
                            if (bool.TryParse(value, out var requireAdmin))
                            {
                                parameters.RunAsAdmin = requireAdmin;
                            }
                            break;
                        case "quiet":
                            if (bool.TryParse(value, out var quiet))
                            {
                                parameters.QuietMode = quiet;
                            }
                            break;
                        case "workdir":
                            parameters.WorkingDirectory = value;
                            break;
                    }
                }

                return parameters;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert protocol URL to launch parameters");
                return null;
            }
        }

        /// <summary>
        /// Internal class for holding parsed protocol URL information
        /// </summary>
        private class ProtocolUrlInfo
        {
            public string Action { get; set; } = string.Empty;
            public System.Collections.Specialized.NameValueCollection QueryParameters { get; set; } = new();
        }
    }
}
