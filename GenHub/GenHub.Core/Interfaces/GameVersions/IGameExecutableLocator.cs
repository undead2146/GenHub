using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces.GameVersions
{
    /// <summary>
    /// Interface for locating and validating game executables
    /// </summary>
    public interface IGameExecutableLocator
    {
        /// <summary>
        /// Find the best game executable in a directory
        /// </summary>
        Task<string> FindBestGameExecutableAsync(string directory, bool preferZeroHour = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Find any executable in a directory
        /// </summary>
        Task<string> FindExecutableAsync(string directory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Find game executable with success indicator
        /// </summary>
        Task<(bool Success, string? ExecutablePath)> FindGameExecutableAsync(string directory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if directory contains Zero Hour installation
        /// </summary>
        bool IsZeroHourDirectory(string directory);

        /// <summary>
        /// Validate if file is a valid game executable
        /// </summary>
        bool IsValidGameExecutable(string filePath);

        /// <summary>
        /// Get executable information from file path
        /// </summary>
        GameVersion GetExecutableInfo(string executablePath);

        /// <summary>
        /// Scan directory for all executable files
        /// </summary>
        List<GameVersion> ScanDirectoryForExecutables(string directory);

        /// <summary>
        /// Locate and validate executable with enhanced metadata
        /// </summary>
        Task<GameExecutableInfo?> LocateExecutableAsync(string installPath, string gameType, CancellationToken cancellationToken = default);
    }
}
