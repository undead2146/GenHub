using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.DesktopShortcuts
{
    /// <summary>
    /// Platform-specific service for creating and managing desktop shortcuts
    /// </summary>
    public interface IShortcutPlatformService
    {
        /// <summary>
        /// Creates a desktop shortcut based on the provided configuration
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with shortcut creation details</returns>
        Task<OperationResult> CreateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a desktop shortcut
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<OperationResult> RemoveShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a shortcut exists and is functional
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        Task<OperationResult<ShortcutValidationResult>> ValidateShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Repairs a broken shortcut by updating its target and properties
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Repair operation result</returns>
        Task<OperationResult> RepairShortcutAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the file path where a shortcut would be created
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <returns>Full path to the shortcut file</returns>
        string GetShortcutPath(ShortcutConfiguration configuration);

        /// <summary>
        /// Checks if the platform supports the specified shortcut type
        /// </summary>
        /// <param name="shortcutType">Type of shortcut</param>
        /// <returns>True if supported, false otherwise</returns>
        bool SupportsShortcutType(ShortcutType shortcutType);

        /// <summary>
        /// Gets platform-specific file extensions for shortcuts
        /// </summary>
        /// <returns>Array of supported file extensions</returns>
        string[] GetSupportedExtensions();
    }
}
