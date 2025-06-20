using System;
using System.Runtime.Serialization;

namespace GenHub.Core.Models.GitHub
{
    /// <summary>
    /// Exception thrown when GitHub service operations fail
    /// </summary>
    [Serializable]
    public class GitHubServiceException : Exception
    {
        /// <summary>
        /// Gets the error code if available
        /// </summary>
        public string? ErrorCode { get; }
        
        /// <summary>
        /// Gets the HTTP status code if available
        /// </summary>
        public int? HttpStatusCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class
        /// </summary>
        public GitHubServiceException() : base("GitHub service error")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public GitHubServiceException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public GitHubServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// and error code
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="errorCode">The error code associated with the error</param>
        public GitHubServiceException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// and HTTP status code
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="httpStatusCode">The HTTP status code associated with the error</param>
        public GitHubServiceException(string message, int httpStatusCode) : base(message)
        {
            HttpStatusCode = httpStatusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// error code, and HTTP status code
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="errorCode">The error code associated with the error</param>
        /// <param name="httpStatusCode">The HTTP status code associated with the error</param>
        public GitHubServiceException(string message, string errorCode, int httpStatusCode) : base(message)
        {
            ErrorCode = errorCode;
            HttpStatusCode = httpStatusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with serialized data
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        protected GitHubServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
        ///
