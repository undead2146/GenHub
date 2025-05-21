using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for locating game executables and detecting game types
    /// </summary>
    public interface IGameExecutableLocator
    {
        /// <summary>
        /// Finds the game executable in a given directory
        /// </summary>
        Task<string> FindExecutableAsync(string directory, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Finds the best executable with proper prioritization based on game variant
        /// </summary>
        Task<string> FindBestGameExecutableAsync(
            string directory, 
            bool preferZeroHour = false, 
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Determines if a directory contains a Zero Hour installation
        /// </summary>
        bool IsZeroHourDirectory(string directory);
        
        /// <summary>
        /// Finds an executable and determines if it was successful
        /// </summary>
        Task<(bool Success, string? ExecutablePath)> FindGameExecutableAsync(
            string directory,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Scans a directory for game executables and returns information about them
        /// </summary>
        /// <param name="directory">Directory to scan</param>
        /// <returns>List of game versions with executable information</returns>
        List<GameVersion> ScanDirectoryForExecutables(string directory);
        
        /// <summary>
        /// Gets information about an executable based on its name
        /// </summary>
        GameVersion GetExecutableInfo(string executablePath);
        
        /// <summary>
        /// Checks if a file is a valid game executable
        /// </summary>
        bool IsValidGameExecutable(string filePath);
    }
}
