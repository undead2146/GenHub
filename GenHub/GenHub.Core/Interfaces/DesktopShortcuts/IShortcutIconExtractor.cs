using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.DesktopShortcuts
{
    /// <summary>
    /// Service for extracting and managing icons for shortcuts
    /// </summary>
    public interface IShortcutIconExtractor
    {
        /// <summary>
        /// Extracts an icon from a game executable
        /// </summary>
        /// <param name="executablePath">Path to the executable</param>
        /// <param name="outputPath">Path where to save the extracted icon</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with the path to the extracted icon</returns>
        Task<OperationResult<string>> ExtractIconFromExecutableAsync(string executablePath, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the best available icon for a profile
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with the path to the best icon</returns>
        Task<OperationResult<string>> GetBestIconForProfileAsync(string profileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a default icon for a profile type
        /// </summary>
        /// <param name="profileType">Type of profile (e.g., "Generals", "Zero Hour")</param>
        /// <param name="outputPath">Path where to save the icon</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result with the path to the created icon</returns>
        Task<OperationResult<string>> CreateDefaultIconAsync(string profileType, string outputPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that an icon file is suitable for shortcuts
        /// </summary>
        /// <param name="iconPath">Path to the icon file</param>
        /// <returns>Validation result</returns>
        OperationResult ValidateIcon(string iconPath);
    }
}
