using System;

namespace GenHub.Core.Models.Results
{
    /// <summary>
    /// Represents the result of a game launch operation
    /// </summary>
    public class LaunchResult : OperationResult
    {
        /// <summary>
        /// The process ID of the launched game, if available
        /// </summary>
        public int? ProcessId { get; init; }

        /// <summary>
        /// The executable path that was launched
        /// </summary>
        public string? ExecutablePath { get; init; }

        /// <summary>
        /// The working directory used for the launch
        /// </summary>
        public string? WorkingDirectory { get; init; }

        /// <summary>
        /// Command line arguments used for the launch
        /// </summary>
        public string? Arguments { get; init; }

        /// <summary>
        /// Whether the game was launched with administrator privileges
        /// </summary>
        public bool LaunchedAsAdmin { get; init; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public LaunchResult()
        {
        }

        /// <summary>
        /// Protected constructor for derived classes
        /// </summary>
        protected LaunchResult(bool success, string? errorMessage = null, Exception? exception = null)
            : base(success, errorMessage, exception)
        {
        }

        /// <summary>
        /// Creates a successful launch result
        /// </summary>
        public static LaunchResult Succeeded(string executablePath, string workingDirectory, string? arguments = null, bool launchedAsAdmin = false, int? processId = null)
        {
            return new LaunchResult
            {
                Success = true,
                ExecutablePath = executablePath,
                WorkingDirectory = workingDirectory,
                Arguments = arguments,
                LaunchedAsAdmin = launchedAsAdmin,
                ProcessId = processId
            };
        }

        /// <summary>
        /// Creates a failed launch result with error message
        /// </summary>
        public static LaunchResult Failed(string errorMessage, Exception? exception = null)
        {
            return new LaunchResult(false, errorMessage, exception);
        }

        /// <summary>
        /// Creates a failed launch result with context information
        /// </summary>
        public static LaunchResult FailedWithContext(string errorMessage, string? executablePath = null, string? workingDirectory = null, Exception? exception = null)
        {
            return new LaunchResult(false, errorMessage, exception)
            {
                ExecutablePath = executablePath,
                WorkingDirectory = workingDirectory
            };
        }
    }
}
