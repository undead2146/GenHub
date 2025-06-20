using System;

namespace GenHub.Core.Models.Results
{
    /// <summary>
    /// Result of game launch preparation containing optimized paths and success status
    /// </summary>
    public class GameLaunchPrepResult
    {
        /// <summary>
        /// Whether the launch preparation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Optimized executable path to use for launching
        /// </summary>
        public string? ExecutablePath { get; set; }
        
        /// <summary>
        /// Optimized working directory to use for launching
        /// </summary>
        public string? WorkingDirectory { get; set; }
        
        /// <summary>
        /// Error message if launch preparation failed
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static GameLaunchPrepResult Succeeded(string exePath, string workingDir)
        {
            return new GameLaunchPrepResult
            {
                Success = true,
                ExecutablePath = exePath,
                WorkingDirectory = workingDir
            };
        }
        
        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static GameLaunchPrepResult Failed(string errorMessage)
        {
            return new GameLaunchPrepResult
            {
                Success = false,
                Message = errorMessage
            };
        }
    }
}
