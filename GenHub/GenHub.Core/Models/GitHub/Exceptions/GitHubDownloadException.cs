using System;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Exception thrown when downloading GitHub artifacts fails
    /// </summary>
    public class GitHubDownloadException : Exception
    {
        /// <summary>
        /// Creates a new instance of GitHubDownloadException
        /// </summary>
        public GitHubDownloadException(string message) : base(message)
        {
        }
        
        /// <summary>
        /// Creates a new instance of GitHubDownloadException with inner exception
        /// </summary>
        public GitHubDownloadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
