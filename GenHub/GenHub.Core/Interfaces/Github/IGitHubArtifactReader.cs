using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Service for GitHub artifact operations
    /// </summary>
    public interface IGitHubArtifactReader
    {
        /// <summary>
        /// Get available workflow artifacts for the configured repository
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetAvailableArtifactsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Download a specific artifact by ID
        /// </summary>
        Task<string> DownloadArtifactAsync(
            long artifactId, 
            string destinationFolder, 
            IProgress<double>? progress = null, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get artifacts for a specific workflow run
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForRunAsync(
            GitHubWorkflow run,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets artifacts for a specific workflow run by run ID
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForWorkflowAsync(
            long runId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get artifacts from a specific repository
        /// </summary>
        Task<IEnumerable<GitHubArtifact>> GetArtifactsForRepositoryAsync(
            GitHubRepository repoConfig,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Download an artifact from a specific repository
        /// </summary>
        Task<string> DownloadArtifactAsync(
            GitHubRepository repoConfig,
            long artifactId,
            string destinationFolder,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses build information from an artifact name
        /// </summary>
        GitHubBuild ParseBuildInfo(string artifactName);

        /// <summary>
        /// Gets the list of detected versions from GitHub artifacts
        /// </summary>
        Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of detected versions from GitHub artifacts for a specific repository
        /// </summary>
        Task<IEnumerable<GameVersion>> GetDetectedVersionsAsync(GitHubRepository repoConfig, CancellationToken cancellationToken = default);
    }
}
