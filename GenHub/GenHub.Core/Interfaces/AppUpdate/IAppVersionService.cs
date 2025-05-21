namespace GenHub.Core.Interfaces.AppUpdate
{
    /// <summary>
    /// Service for retrieving application version information
    /// </summary>
    public interface IAppVersionService
    {
        /// <summary>
        /// Gets the current application version
        /// </summary>
        string GetCurrentVersion();
    }
}
