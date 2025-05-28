using System;
using GenHub.Core.Models;
using GenHub.Core.Models.GameProfiles;

namespace GenHub.Core.Models.Results
{
    /// <summary>
    /// Represents the result of an artifact installation operation
    /// </summary>
    public class InstallationResult : OperationResult<GameVersion>
    {
        /// <summary>
        /// The artifact that was installed
        /// </summary>
        public GitHubArtifact? Artifact { get; init; }
        
        /// <summary>
        /// The game version that was created from the installation
        /// </summary>
        public GameVersion? GameVersion 
        { 
            get => Data; 
            init => Data = value; 
        }

        /// <summary>
        /// The installation directory path
        /// </summary>
        public string? InstallationPath { get; init; }

        /// <summary>
        /// The size of the installed content in bytes
        /// </summary>
        public long InstalledSizeBytes { get; init; }

        /// <summary>
        /// The time taken to complete the installation
        /// </summary>
        public TimeSpan InstallationDuration { get; init; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public InstallationResult()
        {
        }

        /// <summary>
        /// Protected constructor for derived classes
        /// </summary>
        protected InstallationResult(bool success, string? errorMessage = null, Exception? exception = null)
            : base(success, errorMessage, exception)
        {
        }

        /// <summary>
        /// Creates a successful installation result
        /// </summary>
        public static InstallationResult Succeeded(
            GitHubArtifact artifact, 
            GameVersion gameVersion, 
            string? installationPath = null,
            long installedSizeBytes = 0,
            TimeSpan installationDuration = default)
        {
            return new InstallationResult
            {
                Success = true,
                Artifact = artifact,
                GameVersion = gameVersion,
                InstallationPath = installationPath,
                InstalledSizeBytes = installedSizeBytes,
                InstallationDuration = installationDuration
            };
        }

        /// <summary>
        /// Creates a failed installation result
        /// </summary>
        public static InstallationResult Failed(
            string errorMessage, 
            GitHubArtifact? artifact = null, 
            Exception? exception = null)
        {
            return new InstallationResult(false, errorMessage, exception)
            {
                Artifact = artifact
            };
        }

        /// <summary>
        /// Creates a failed installation result with context
        /// </summary>
        public static InstallationResult FailedWithContext(
            string errorMessage,
            GitHubArtifact artifact,
            string? installationPath = null,
            Exception? exception = null)
        {
            return new InstallationResult(false, errorMessage, exception)
            {
                Artifact = artifact,
                InstallationPath = installationPath
            };
        }
    }
}
