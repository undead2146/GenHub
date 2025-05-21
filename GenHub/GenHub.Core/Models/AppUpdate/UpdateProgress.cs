public class UpdateProgress
{
    /// <summary>
    /// Gets or sets the current status of the update process.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the percentage of the update that has been completed.
    /// </summary>
    public double PercentageCompleted { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets a message providing additional information about the update progress.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the update is currently in progress.
    /// </summary>
    public bool IsInProgress { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the update was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = false;
}