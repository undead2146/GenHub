using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.AdvancedLauncher
{
    /// <summary>
    /// Service for direct launching of games without showing the main UI
    /// </summary>
    public interface IDirectLaunchService
    {
        /// <summary>
        /// Launches a game profile directly using the provided parameters
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Launch result</returns>
        Task<OperationResult<QuickLaunchResult>> LaunchDirectlyAsync(LaunchParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a profile can be launched with the given parameters
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        Task<OperationResult<LaunchValidationResult>> ValidateLaunchAsync(LaunchParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a diagnostic launch that provides detailed information about the launch process
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Diagnostic launch result</returns>
        Task<OperationResult<QuickLaunchResult>> DiagnosticLaunchAsync(LaunchParameters parameters, CancellationToken cancellationToken = default);
    }
}
