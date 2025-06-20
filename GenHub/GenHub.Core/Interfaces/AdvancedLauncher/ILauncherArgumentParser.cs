using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.AdvancedLauncher
{
    /// <summary>
    /// Service for parsing command line arguments for advanced launcher functionality
    /// </summary>
    public interface ILauncherArgumentParser
    {
        /// <summary>
        /// Parses command line arguments into launch parameters
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Parsed launch parameters or error information</returns>
        OperationResult<LaunchParameters> ParseArguments(string[] args);

        /// <summary>
        /// Validates that the provided launch parameters are valid
        /// </summary>
        /// <param name="parameters">Launch parameters to validate</param>
        /// <returns>Validation result</returns>
        OperationResult ValidateParameters(LaunchParameters parameters);

        /// <summary>
        /// Builds command line arguments from launch parameters
        /// </summary>
        /// <param name="parameters">Launch parameters</param>
        /// <returns>Command line argument string</returns>
        string BuildArgumentString(LaunchParameters parameters);

        /// <summary>
        /// Gets help text for command line usage
        /// </summary>
        /// <returns>Help text string</returns>
        string GetHelpText();
    }
}
