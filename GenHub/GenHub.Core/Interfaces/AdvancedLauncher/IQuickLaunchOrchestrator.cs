using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.AdvancedLauncher
{
    /// <summary>
    /// Orchestrator service that coordinates all advanced launcher functionality
    /// </summary>
    public interface IQuickLaunchOrchestrator
    {
        /// <summary>
        /// Processes command line arguments and executes the appropriate action
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result indicating success or failure</returns>
        Task<OperationResult> ProcessCommandLineAsync(string[] args, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles a protocol URL request
        /// </summary>
        /// <param name="protocolUrl">Protocol URL to handle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> HandleProtocolRequestAsync(string protocolUrl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines if the application should exit after processing the command line
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>True if the application should exit, false if it should continue normally</returns>
        bool ShouldExitAfterProcessing(string[] args);

        /// <summary>
        /// Gets the current launch context if a launch is in progress
        /// </summary>
        /// <returns>Current launch context or null</returns>
        LaunchContext? GetCurrentLaunchContext();
    }
}
