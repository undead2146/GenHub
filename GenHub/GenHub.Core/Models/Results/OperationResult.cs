using System;

namespace GenHub.Core.Models.Results
{
    /// <summary>
    /// Generic result type for operations that can succeed or fail
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// Any exception that occurred during the operation
        /// </summary>
        public Exception? Exception { get; set; }
        
        /// <summary>
        /// The time when the operation completed
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Default constructor
        /// </summary>
        public OperationResult()
        {
        }

        /// <summary>
        /// Constructor for derived classes
        /// </summary>
        protected OperationResult(bool success, string? message = null, Exception? exception = null)
        {
            Success = success;
            Message = message;
            Exception = exception;
            CompletedAt = DateTime.Now;
        }
        
        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static OperationResult Succeeded()
        {
            return new OperationResult { Success = true };
        }
                /// <summary>
        /// Creates a successful result
        /// </summary>
        public static OperationResult Succeeded(string message)
        {
            return new OperationResult
            {
                Success = true,
                Message = message};
        }
        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static OperationResult Failed(string message, Exception? exception = null)
        {
            return new OperationResult 
            { 
                Success = false, 
                Message = message,
                Exception = exception
            };
        }
    }
    
    /// <summary>
    /// Generic result type for operations that return data
    /// </summary>
    public class OperationResult<T> : OperationResult
    {
        /// <summary>
        /// The data returned by the operation
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public OperationResult()
        {
        }

        /// <summary>
        /// Constructor for derived classes
        /// </summary>
        protected OperationResult(bool success, string? message = null, Exception? exception = null)
            : base(success, message, exception)
        {
        }
        
        /// <summary>
        /// Creates a successful result with data
        /// </summary>
        public static OperationResult<T> Succeeded(T data)
        {
            return new OperationResult<T> { Success = true, Data = data };
        }
        
        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static new OperationResult<T> Failed(string message, Exception? exception = null)
        {
            return new OperationResult<T> 
            { 
                Success = false, 
                Message = message,
                Exception = exception,
                Data = default
            };
        }
    }
}
