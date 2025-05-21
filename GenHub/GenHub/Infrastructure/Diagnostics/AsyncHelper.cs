using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GenHub.Infrastructure.Diagnostics
{
    /// <summary>
    /// Helper class for executing async operations safely
    /// </summary>
    public static class AsyncHelper
    {
        /// <summary>
        /// Execute an async operation with timeout protection and logging
        /// </summary>
        public static async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<CancellationToken, Task<T>> asyncOperation,
            TimeSpan timeout,
            string operationName,
            ILogger logger,
            T defaultValue = default)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogDebug("Starting {OperationName}", operationName);
            
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                var result = await asyncOperation(cts.Token);
                
                stopwatch.Stop();
                logger.LogDebug("{OperationName} completed in {ElapsedMs}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
                
                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                logger.LogWarning("{OperationName} timed out after {ElapsedMs}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
                
                return defaultValue;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex, "{OperationName} failed after {ElapsedMs}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
                
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Execute an async operation with timeout protection and logging (non-returning version)
        /// </summary>
        public static async Task ExecuteWithTimeoutAsync(
            Func<CancellationToken, Task> asyncOperation,
            TimeSpan timeout,
            string operationName,
            ILogger logger)
        {
            var stopwatch = Stopwatch.StartNew();
            logger.LogDebug("Starting {OperationName}", operationName);
            
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                await asyncOperation(cts.Token);
                
                stopwatch.Stop();
                logger.LogDebug("{OperationName} completed in {ElapsedMs}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                logger.LogWarning("{OperationName} timed out after {ElapsedMs}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex, "{OperationName} failed after {ElapsedMs}ms", 
                    operationName, stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// Fire and forget helper with error logging
        /// </summary>
        public static void FireAndForget(Func<Task> asyncOperation, ILogger logger, string operationName)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await asyncOperation();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in fire-and-forget {OperationName}", operationName);
                }
            });
        }
    }
}
