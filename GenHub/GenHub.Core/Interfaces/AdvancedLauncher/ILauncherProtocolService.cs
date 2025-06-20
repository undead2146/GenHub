using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.AdvancedLauncher
{
    /// <summary>
    /// Service for handling genhub:// protocol URLs and system integration
    /// </summary>
    public interface ILauncherProtocolService
    {
        /// <summary>
        /// Registers the genhub:// protocol with the operating system
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Registration result</returns>
        Task<OperationResult> RegisterProtocolAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregisters the genhub:// protocol from the operating system
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Unregistration result</returns>
        Task<OperationResult> UnregisterProtocolAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles a protocol URL (genhub://...)
        /// </summary>
        /// <param name="protocolUrl">The protocol URL to handle</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> HandleProtocolUrlAsync(string protocolUrl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the protocol is currently registered
        /// </summary>
        /// <returns>True if registered, false otherwise</returns>
        Task<bool> IsProtocolRegisteredAsync();

        /// <summary>
        /// Builds a protocol URL for the given action and parameters
        /// </summary>
        /// <param name="action">Protocol action (e.g., "launch", "create-shortcut")</param>
        /// <param name="parameters">Additional parameters</param>
        /// <returns>Formatted protocol URL</returns>
        string BuildProtocolUrl(string action, params (string key, string value)[] parameters);
    }
}
