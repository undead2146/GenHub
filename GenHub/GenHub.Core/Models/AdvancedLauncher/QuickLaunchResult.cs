using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Represents the result of a quick launch operation
    /// </summary>
    public class QuickLaunchResult
    {
        /// <summary>
        /// Gets or sets whether the launch was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the profile ID that was launched
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// Gets or sets the profile name that was launched
        /// </summary>
        public string? ProfileName { get; set; }

        /// <summary>
        /// Gets or sets the process ID of the launched game (if successful)
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the time the launch was initiated
        /// </summary>
        public DateTime LaunchTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the time taken to complete the launch operation
        /// </summary>
        public TimeSpan LaunchDuration { get; set; }

        /// <summary>
        /// Gets or sets any error message if the launch failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets detailed diagnostic information about the launch
        /// </summary>
        public List<string> DiagnosticInfo { get; set; } = new();

        /// <summary>
        /// Gets or sets the executable path that was launched
        /// </summary>
        public string? ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the working directory used for the launch
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the command line arguments used
        /// </summary>
        public string? CommandLineArguments { get; set; }

        /// <summary>
        /// Gets or sets whether administrative privileges were used
        /// </summary>
        public bool UsedAdminPrivileges { get; set; }

        /// <summary>
        /// Gets or sets any warnings generated during the launch
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets performance metrics for the launch operation
        /// </summary>
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();        /// <summary>
        /// Creates a successful launch result
        /// </summary>
        /// <param name="profileId">Profile ID that was launched</param>
        /// <param name="processId">Process ID of the launched game</param>
        /// <returns>Successful launch result</returns>
        public static QuickLaunchResult Succeeded(string profileId, int processId)
        {
            return new QuickLaunchResult
            {
                Success = true,
                ProfileId = profileId,
                ProcessId = processId
            };
        }

        /// <summary>
        /// Creates a failed launch result
        /// </summary>
        /// <param name="profileId">Profile ID that failed to launch</param>
        /// <param name="errorMessage">Error message describing the failure</param>
        /// <returns>Failed launch result</returns>
        public static QuickLaunchResult Failed(string profileId, string errorMessage)
        {
            return new QuickLaunchResult
            {
                Success = false,
                ProfileId = profileId,
                ErrorMessage = errorMessage
            };
        }
    }
}
