using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Interfaces.AdvancedLauncher;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AdvancedLauncher.Services
{
    /// <summary>
    /// Orchestrates quick launch operations by coordinating between different launch services
    /// </summary>
    public class QuickLaunchOrchestrator : IQuickLaunchOrchestrator
    {
        private readonly IDirectLaunchService _directLaunchService;
        private readonly ILauncherProtocolService _protocolService;
        private readonly ILauncherArgumentParser _argumentParser;
        private readonly ILogger<QuickLaunchOrchestrator> _logger;
        private LaunchContext? _currentLaunchContext;

        public QuickLaunchOrchestrator(
            IDirectLaunchService directLaunchService,
            ILauncherProtocolService protocolService,
            ILauncherArgumentParser argumentParser,
            ILogger<QuickLaunchOrchestrator> logger)
        {
            _directLaunchService = directLaunchService ?? throw new ArgumentNullException(nameof(directLaunchService));
            _protocolService = protocolService ?? throw new ArgumentNullException(nameof(protocolService));
            _argumentParser = argumentParser ?? throw new ArgumentNullException(nameof(argumentParser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes command line arguments and executes the appropriate action
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result indicating success or failure</returns>
        public async Task<OperationResult> ProcessCommandLineAsync(string[] args, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing command line with {ArgCount} arguments", args.Length);

                // Parse command line arguments
                var parseResult = _argumentParser.ParseArguments(args);
                if (!parseResult.Success || parseResult.Data == null)
                {
                    _logger.LogWarning("Failed to parse launch arguments: {Message}", parseResult.Message);
                    return OperationResult.Failed($"Invalid arguments: {parseResult.Message}");
                }

                var parameters = parseResult.Data;
                _logger.LogInformation("Parsed launch parameters: Mode={Mode}, ProfileId={ProfileId}", 
                    parameters.Mode, parameters.ProfileId);                // Create launch context
                _currentLaunchContext = new LaunchContext
                {
                    Id = Guid.NewGuid().ToString(),
                    Parameters = parameters,
                    StartTime = DateTime.UtcNow
                };

                // Perform pre-launch validation
                var validationResult = await _directLaunchService.ValidateLaunchAsync(parameters, cancellationToken);
                if (!validationResult.Success)
                {
                    _logger.LogWarning("Launch validation failed: {Message}", validationResult.Message);
                    return OperationResult.Failed($"Validation failed: {validationResult.Message}");
                }

                // Execute the launch based on the mode
                switch (parameters.Mode)
                {
                    case LaunchMode.Quick:
                        return await LaunchQuickAsync(parameters, cancellationToken);
                    
                    case LaunchMode.Diagnostic:
                        return await LaunchDiagnosticAsync(parameters, cancellationToken);
                    
                    case LaunchMode.Validate:
                        return await ValidateOnlyAsync(parameters, cancellationToken);
                    
                    case LaunchMode.Normal:
                        // For normal mode, we don't launch directly but return success
                        // to let the UI handle the launch
                        return OperationResult.Succeeded("Normal launch mode - UI will handle launch");
                    
                    default:
                        _logger.LogWarning("Unsupported launch mode: {Mode}", parameters.Mode);
                        return OperationResult.Failed($"Unsupported launch mode: {parameters.Mode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during command line processing");
                return OperationResult.Failed($"Launch failed: {ex.Message}");
            }
            finally
            {
                // Clear launch context after processing
                _currentLaunchContext = null;
            }
        }

        /// <summary>
        /// Handles a protocol URL request
        /// </summary>
        /// <param name="protocolUrl">Protocol URL to handle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        public async Task<OperationResult> HandleProtocolRequestAsync(string protocolUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing protocol request: {Url}", protocolUrl);

                var result = await _protocolService.HandleProtocolUrlAsync(protocolUrl, cancellationToken);
                
                if (result.Success)
                {
                    _logger.LogInformation("Protocol request completed successfully");
                }
                else
                {
                    _logger.LogWarning("Protocol request failed: {Message}", result.Message);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during protocol request processing");
                return OperationResult.Failed($"Protocol request failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if the application should exit after processing the command line
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>True if the application should exit, false if it should continue normally</returns>
        public bool ShouldExitAfterProcessing(string[] args)
        {
            // Parse arguments to determine if this is a quick launch that should exit
            var parseResult = _argumentParser.ParseArguments(args);
            if (parseResult.Success && parseResult.Data != null)
            {
                // Exit after processing for quick launch modes
                return parseResult.Data.Mode is LaunchMode.Quick or LaunchMode.Diagnostic or LaunchMode.Validate;
            }

            // Don't exit by default
            return false;
        }

        /// <summary>
        /// Gets the current launch context if a launch is in progress
        /// </summary>
        /// <returns>Current launch context or null</returns>
        public LaunchContext? GetCurrentLaunchContext()
        {
            return _currentLaunchContext;
        }

        private async Task<OperationResult> LaunchQuickAsync(LaunchParameters parameters, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performing quick launch for profile: {ProfileId}", parameters.ProfileId);

            var result = await _directLaunchService.LaunchDirectlyAsync(parameters, cancellationToken);
            
            if (result.Success && result.Data != null)
            {
                _logger.LogInformation("Quick launch completed successfully: {ProfileId}", parameters.ProfileId);
                return OperationResult.Succeeded($"Profile launched: {parameters.ProfileId}");
            }
            else
            {
                _logger.LogWarning("Quick launch failed: {Message}", result.Message);
                return OperationResult.Failed(result.Message ?? "Quick launch failed");
            }
        }

        private async Task<OperationResult> LaunchDiagnosticAsync(LaunchParameters parameters, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performing diagnostic launch for profile: {ProfileId}", parameters.ProfileId);

            var result = await _directLaunchService.DiagnosticLaunchAsync(parameters, cancellationToken);
            
            if (result.Success && result.Data != null)
            {
                _logger.LogInformation("Diagnostic launch completed successfully: {ProfileId}", parameters.ProfileId);
                return OperationResult.Succeeded($"Diagnostic launch completed: {parameters.ProfileId}");
            }
            else
            {
                _logger.LogWarning("Diagnostic launch failed: {Message}", result.Message);
                return OperationResult.Failed(result.Message ?? "Diagnostic launch failed");
            }
        }

        private async Task<OperationResult> ValidateOnlyAsync(LaunchParameters parameters, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performing validation for profile: {ProfileId}", parameters.ProfileId);

            var result = await _directLaunchService.ValidateLaunchAsync(parameters, cancellationToken);
            
            if (result.Success && result.Data != null)
            {
                _logger.LogInformation("Validation completed successfully: {ProfileId}", parameters.ProfileId);
                return OperationResult.Succeeded($"Validation successful: {parameters.ProfileId}");
            }
            else
            {
                _logger.LogWarning("Validation failed: {Message}", result.Message);
                return OperationResult.Failed(result.Message ?? "Validation failed");
            }
        }
    }
}
