using System;

namespace GenHub.Core.Models.Results
{
    /// <summary>
    /// Model for tracking installation progress
    /// </summary>
    public class InstallProgress
    {
        /// <summary>
        /// Current percentage of completion (0-100)
        /// </summary>
        public double Percentage { get; set; }
        
        /// <summary>
        /// Description of the current operation
        /// </summary>
        public string CurrentOperation { get; set; } = string.Empty;
        
        /// <summary>
        /// Current installation stage
        /// </summary>
        public InstallStage Stage { get; set; }
        
        /// <summary>
        /// Any error message if applicable
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Optional detailed message about current operation
        /// </summary>
        public string? DetailMessage { get; set; }
        
        /// <summary>
        /// Bytes processed in the current operation
        /// </summary>
        public long BytesProcessed { get; set; }
        
        /// <summary>
        /// Total bytes to process in the current operation
        /// </summary>
        public long TotalBytes { get; set; }
        
        /// <summary>
        /// Number of files processed
        /// </summary>
        public int FilesProcessed { get; set; }
        
        /// <summary>
        /// Total number of files to process
        /// </summary>
        public int TotalFiles { get; set; }
        
        /// <summary>
        /// Whether the installation has completed
        /// </summary>
        public bool IsCompleted { get; set; }
        
        /// <summary>
        /// Whether the installation has encountered an error
        /// </summary>
        public bool HasError { get => !string.IsNullOrEmpty(ErrorMessage) || _hasError; set => _hasError = value; }
        private bool _hasError;
        
        /// <summary>
        /// Elapsed time since installation started
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }
        
        /// <summary>
        /// Status message for the current operation
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Current progress value for the operation (e.g., bytes downloaded)
        /// </summary>
        public long Current { get; set; }
        
        /// <summary>
        /// Total units to process (e.g., total file size)
        /// </summary>
        public long Total { get; set; }
        
        /// <summary>
        /// Creates a download stage progress update
        /// </summary>
        public static InstallProgress DownloadProgress(double percentage, long bytesDownloaded, long totalBytes)
        {
            return new InstallProgress
            {
                Stage = InstallStage.Downloading,
                Percentage = percentage,
                CurrentOperation = "Downloading",
                BytesProcessed = bytesDownloaded,
                TotalBytes = totalBytes,
                Current = bytesDownloaded,
                Total = totalBytes,
                Message = "Downloading..."
            };
        }
        
        /// <summary>
        /// Creates an extraction stage progress update
        /// </summary>
        public static InstallProgress ExtractionProgress(double percentage, int filesExtracted, int totalFiles)
        {
            return new InstallProgress
            {
                Stage = InstallStage.Extracting,
                Percentage = percentage,
                CurrentOperation = "Extracting",
                FilesProcessed = filesExtracted,
                TotalFiles = totalFiles,
                Current = filesExtracted,
                Total = totalFiles,
                Message = "Extracting files..."
            };
        }
        
        /// <summary>
        /// Creates a verification stage progress update
        /// </summary>
        public static InstallProgress VerificationProgress(double percentage, string detailMessage = "")
        {
            return new InstallProgress
            {
                Stage = InstallStage.Verifying,
                Percentage = percentage,
                CurrentOperation = "Verifying",
                DetailMessage = detailMessage,
                Message = $"Verifying: {detailMessage}"
            };
        }
        
        /// <summary>
        /// Creates an error progress update
        /// </summary>
        public static InstallProgress Error(string errorMessage)
        {
            return new InstallProgress
            {
                Stage = InstallStage.Error,
                Percentage = 100,
                CurrentOperation = "Error",
                ErrorMessage = errorMessage,
                _hasError = true,
                Message = $"Error: {errorMessage}"
            };
        }
        
        /// <summary>
        /// Creates a completed progress update
        /// </summary>
        public static InstallProgress Completed(TimeSpan elapsed)
        {
            return new InstallProgress
            {
                Stage = InstallStage.Completed,
                Percentage = 100,
                CurrentOperation = "Completed",
                IsCompleted = true,
                ElapsedTime = elapsed,
                Message = "Installation completed successfully"
            };
        }
    }
}
