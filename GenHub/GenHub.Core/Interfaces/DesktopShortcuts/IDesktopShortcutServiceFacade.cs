using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.DesktopShortcuts
{
    /// <summary>
    /// Facade service that coordinates all desktop shortcut functionality
    /// </summary>
    public interface IDesktopShortcutServiceFacade
    {
        /// <summary>
        /// Creates a desktop shortcut for the specified profile
        /// </summary>
        /// <param name="profileId">Profile ID to create shortcut for</param>
        /// <param name="configuration">Shortcut configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> CreateShortcutAsync(string profileId, ShortcutConfiguration? configuration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates desktop shortcuts for multiple profiles
        /// </summary>
        /// <param name="profileIds">Profile IDs to create shortcuts for</param>
        /// <param name="configuration">Shortcut configuration template</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with details about each shortcut</returns>
        Task<OperationResult> CreateBulkShortcutsAsync(string[] profileIds, ShortcutConfiguration? configuration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a desktop shortcut for the specified profile
        /// </summary>
        /// <param name="profileId">Profile ID to remove shortcut for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> RemoveShortcutAsync(string profileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates all existing shortcuts and reports any issues
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation results for all shortcuts</returns>
        Task<OperationResult<ShortcutValidationSummary>> ValidateAllShortcutsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Repairs broken shortcuts by updating their targets and arguments
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Repair operation result</returns>
        Task<OperationResult> RepairShortcutsAsync(CancellationToken cancellationToken = default);
    }
}
