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
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class
        /// </summary>
        public GitHubServiceException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public GitHubServiceException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public GitHubServiceException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubServiceException"/> class with serialized data
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        protected GitHubServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
        /// 
