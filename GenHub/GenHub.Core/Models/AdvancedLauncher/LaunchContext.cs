using System;
using System.Collections.Generic;

namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Represents the context of a launch operation in progress
    /// </summary>
    public class LaunchContext
    {
        /// <summary>
        /// Gets or sets the unique identifier for this launch context
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the launch parameters
        /// </summary>
        public LaunchParameters Parameters { get; set; } = new();

        /// <summary>
        /// Gets or sets the profile being launched
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// Gets or sets the current stage of the launch process
        /// </summary>
        public LaunchStage CurrentStage { get; set; } = LaunchStage.Initializing;

        /// <summary>
        /// Gets or sets the start time of the launch operation
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the end time of the launch operation (if completed)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets whether the launch operation has completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets whether the launch was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets any error that occurred during launch
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the process ID of the launched application (if successful)
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Gets or sets progress information for the launch operation
        /// </summary>
        public List<string> ProgressLog { get; set; } = new();

        /// <summary>
        /// Gets or sets performance metrics for the launch
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();

        /// <summary>
        /// Gets the total duration of the launch operation
        /// </summary>
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? DateTime.UtcNow.Subtract(StartTime);

        /// <summary>
        /// Marks the launch context as completed
        /// </summary>
        /// <param name="successful">Whether the launch was successful</param>
        /// <param name="error">Error message if unsuccessful</param>
        public void Complete(bool successful, string? error = null)
        {
            IsCompleted = true;
            IsSuccessful = successful;
            Error = error;
            EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds a progress message to the launch context
        /// </summary>
        /// <param name="message">Progress message</param>
        public void AddProgress(string message)
        {
            ProgressLog.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// Updates the current stage of the launch operation
        /// </summary>
        /// <param name="stage">New launch stage</param>
        public void UpdateStage(LaunchStage stage)
        {
            CurrentStage = stage;
            AddProgress($"Stage: {stage}");
        }
    }

    /// <summary>
    /// Defines the stages of a launch operation
    /// </summary>
    public enum LaunchStage
    {
        Initializing,
        ValidatingParameters,
        LoadingProfile,
        ValidatingProfile,
        PreparingLaunch,
        Launching,
        WaitingForProcess,
        Completed,
        Failed
    }
}
