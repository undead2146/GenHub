using GenHub.Core.Helpers;
using GenHub.Core.Models.Content;

namespace GenHub.Core.Extensions;

/// <summary>
/// Extension methods for ContentAcquisitionProgress.
/// </summary>
public static class ContentAcquisitionProgressExtensions
{
    /// <summary>
    /// Formats a user-friendly progress status message with stage indicators.
    /// </summary>
    /// <param name="progress">The progress object to format.</param>
    /// <returns>A formatted progress status string.</returns>
    public static string FormatProgressStatus(this ContentAcquisitionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        // Use the new staged progress format if available
        if (progress.TotalStages > 0 && progress.CurrentStage > 0)
        {
            string stagePart = $"{progress.CurrentStage}/{progress.TotalStages}";
            string description = !string.IsNullOrEmpty(progress.CurrentOperation) &&
                                 !string.Equals(progress.CurrentOperation, progress.StageDescription, StringComparison.Ordinal)
                ? $"{progress.StageDescription}: {progress.CurrentOperation}"
                : progress.StageDescription;

            // Add percentage for stages that have measurable progress
            string percentPart = progress.StageProgress is > 0 and < 100
                ? $" ({progress.StageProgress:F0}%)"
                : string.Empty;

            // Add bottleneck indicator if applicable
            string bottleneckPart = progress.IsBottleneck && !string.IsNullOrEmpty(progress.BottleneckReason)
                ? $" - {progress.BottleneckReason}"
                : string.Empty;

            // Add file count if processing multiple files
            string filesPart = progress.TotalFiles > 1
                ? $" [{progress.FilesProcessed}/{progress.TotalFiles}]"
                : string.Empty;

            return $"{stagePart} - {description}{percentPart}{filesPart}{bottleneckPart}";
        }

        // Fallback to phase-based format
        string phaseName = progress.Phase switch
        {
            ContentAcquisitionPhase.None => "Processing",
            ContentAcquisitionPhase.Downloading => "Downloading",
            ContentAcquisitionPhase.Extracting => "Extracting",
            ContentAcquisitionPhase.Copying => "Copying",
            ContentAcquisitionPhase.ValidatingManifest => "Validating manifest",
            ContentAcquisitionPhase.ValidatingFiles => "Validating files",
            ContentAcquisitionPhase.Delivering => "Installing",
            ContentAcquisitionPhase.StoringInCas => "Storing",
            ContentAcquisitionPhase.Completed => "Complete",
            _ => "Processing",
        };

        if (!string.IsNullOrEmpty(progress.CurrentOperation))
        {
            return $"{phaseName}: {progress.CurrentOperation}";
        }

        string percentText = progress.ProgressPercentage > 0 ? $"{progress.ProgressPercentage:F0}%" : string.Empty;

        if (progress.TotalBytes > 0 && progress.Phase == ContentAcquisitionPhase.Downloading)
        {
            string downloaded = ByteFormatHelper.FormatBytes(progress.BytesProcessed);
            string total = ByteFormatHelper.FormatBytes(progress.TotalBytes);
            return $"{phaseName}: {downloaded} / {total} ({percentText})";
        }

        if (progress.TotalFiles > 0)
        {
            int phasePercent = progress.TotalFiles > 0
                ? (int)((double)progress.FilesProcessed / progress.TotalFiles * 100)
                : 0;
            return $"{phaseName}: {progress.FilesProcessed}/{progress.TotalFiles} files ({phasePercent}%)";
        }

        return !string.IsNullOrEmpty(percentText) ? $"{phaseName}... {percentText}" : $"{phaseName}...";
    }
}
