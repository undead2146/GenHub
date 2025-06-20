using System;

namespace GenHub.Core.Models.GitHub.Exceptions
{
    /// <summary>
    /// Exception thrown when a GitHub download operation fails
    /// </summary>
    public class GitHubDownloadException : GitHubServiceException
    {
        /// <summary>
        /// Gets the artifact ID that failed to download
        /// </summary>
        public long? ArtifactId { get; }
        
        /// <summary>
        /// Gets the download URL that failed
        /// </summary>
        public string? DownloadUrl { get; }
        
        /// <summary>
        /// Gets the HTTP status code if available
        /// </summary>
        public int? HttpStatusCode { get; }

        public GitHubDownloadException() : base("GitHub download failed")
        {
        }

        public GitHubDownloadException(string message) : base(message)
        {
        }

        public GitHubDownloadException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public GitHubDownloadException(long artifactId, string message) : base(message)
        {
            ArtifactId = artifactId;
        }

        public GitHubDownloadException(long artifactId, string message, Exception innerException) : base(message, innerException)
        {
            ArtifactId = artifactId;
        }

        public GitHubDownloadException(string downloadUrl, string message, int httpStatusCode) : base(message)
        {
            DownloadUrl = downloadUrl;
            HttpStatusCode = httpStatusCode;
        }

        public GitHubDownloadException(long artifactId, string downloadUrl, string message, int httpStatusCode) : base(message)
        {
            ArtifactId = artifactId;
            DownloadUrl = downloadUrl;
            HttpStatusCode = httpStatusCode;
        }
    }
}
