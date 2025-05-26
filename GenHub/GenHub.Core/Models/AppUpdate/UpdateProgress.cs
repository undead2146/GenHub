using System;

namespace GenHub.Core.Models.AppUpdate
{
    /// <summary>
    /// Represents the progress of an update operation
    /// </summary>
    public class UpdateProgress
    {
        /// <summary>
        /// Gets or sets the current status message
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current stage of the update
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the percentage complete (0-100)
        /// </summary>
        public int PercentComplete { get; set; }

        /// <summary>
        /// Gets or sets the percentage completed as decimal (0.0-1.0)
        /// </summary>
        public double PercentageCompleted { get; set; }

        /// <summary>
        /// Gets or sets whether the operation is currently in progress
        /// </summary>
        public bool IsInProgress { get; set; }

        /// <summary>
        /// Gets or sets whether the operation completed successfully
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets any additional message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets any error that occurred
        /// </summary>
        public Exception? Error { get; set; }
    }
}
