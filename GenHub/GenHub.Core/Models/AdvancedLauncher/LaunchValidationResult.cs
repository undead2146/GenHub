using System;
using System.Collections.Generic;

namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Represents the result of validating a launch operation
    /// </summary>
    public class LaunchValidationResult
    {
        /// <summary>
        /// Gets or sets whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the profile ID that was validated
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// Gets or sets the profile name that was validated
        /// </summary>
        public string? ProfileName { get; set; }

        /// <summary>
        /// Gets or sets validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets validation information messages
        /// </summary>
        public List<string> Information { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the executable file exists
        /// </summary>
        public bool ExecutableExists { get; set; }

        /// <summary>
        /// Gets or sets whether the data directory is valid
        /// </summary>
        public bool DataDirectoryValid { get; set; }

        /// <summary>
        /// Gets or sets whether the profile configuration is valid
        /// </summary>
        public bool ProfileConfigurationValid { get; set; }

        /// <summary>
        /// Gets or sets whether required dependencies are available
        /// </summary>
        public bool DependenciesAvailable { get; set; }

        /// <summary>
        /// Gets or sets whether the user has necessary permissions
        /// </summary>
        public bool PermissionsValid { get; set; }

        /// <summary>
        /// Gets or sets the validation level that was performed
        /// </summary>
        public LaunchValidation ValidationLevel { get; set; }

        /// <summary>
        /// Gets or sets the time when validation was performed
        /// </summary>
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets additional validation details
        /// </summary>
        public Dictionary<string, object> ValidationDetails { get; set; } = new();

        /// <summary>
        /// Adds an error to the validation result
        /// </summary>
        /// <param name="error">Error message</param>
        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// Adds a warning to the validation result
        /// </summary>
        /// <param name="warning">Warning message</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// Adds information to the validation result
        /// </summary>
        /// <param name="info">Information message</param>
        public void AddInformation(string info)
        {
            Information.Add(info);
        }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <param name="profileId">Profile ID that was validated</param>
        /// <returns>Successful validation result</returns>
        public static LaunchValidationResult Success(string profileId)
        {
            return new LaunchValidationResult
            {
                IsValid = true,
                ProfileId = profileId,
                ExecutableExists = true,
                DataDirectoryValid = true,
                ProfileConfigurationValid = true,
                DependenciesAvailable = true,
                PermissionsValid = true
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="profileId">Profile ID that failed validation</param>
        /// <param name="error">Primary error message</param>
        /// <returns>Failed validation result</returns>
        public static LaunchValidationResult Failure(string profileId, string error)
        {
            var result = new LaunchValidationResult
            {
                IsValid = false,
                ProfileId = profileId
            };
            result.AddError(error);
            return result;
        }
    }
}
