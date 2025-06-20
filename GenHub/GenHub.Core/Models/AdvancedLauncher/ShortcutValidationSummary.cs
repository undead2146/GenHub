using System;
using System.Collections.Generic;
using System.Linq;

namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Summary of shortcut validation results
    /// </summary>
    public class ShortcutValidationSummary
    {
        /// <summary>
        /// Gets or sets the total number of shortcuts validated
        /// </summary>
        public int TotalShortcuts { get; set; }

        /// <summary>
        /// Gets or sets the number of valid shortcuts
        /// </summary>
        public int ValidShortcuts { get; set; }

        /// <summary>
        /// Gets or sets the number of invalid shortcuts
        /// </summary>
        public int InvalidShortcuts { get; set; }

        /// <summary>
        /// Gets or sets the number of shortcuts that were repaired
        /// </summary>
        public int RepairedShortcuts { get; set; }

        /// <summary>
        /// Gets or sets validation results for individual shortcuts
        /// </summary>
        public List<ShortcutValidationResult> Results { get; set; } = new();

        /// <summary>
        /// Gets or sets when the validation was performed
        /// </summary>
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the total time taken for validation
        /// </summary>
        public TimeSpan ValidationDuration { get; set; }

        /// <summary>
        /// Gets whether all shortcuts are valid
        /// </summary>
        public bool AllValid => InvalidShortcuts == 0;

        /// <summary>
        /// Gets the percentage of valid shortcuts
        /// </summary>
        public double ValidPercentage => TotalShortcuts > 0 ? (ValidShortcuts * 100.0) / TotalShortcuts : 100.0;

        /// <summary>
        /// Adds a validation result to the summary
        /// </summary>
        /// <param name="result">Validation result to add</param>
        public void AddResult(ShortcutValidationResult result)
        {
            Results.Add(result);
            TotalShortcuts = Results.Count;
            ValidShortcuts = Results.Count(r => r.IsValid);
            InvalidShortcuts = Results.Count(r => !r.IsValid);
            RepairedShortcuts = Results.Count(r => r.WasRepaired);
        }

        /// <summary>
        /// Gets shortcuts that failed validation
        /// </summary>
        /// <returns>List of invalid shortcuts</returns>
        public List<ShortcutValidationResult> GetInvalidShortcuts()
        {
            return Results.Where(r => !r.IsValid).ToList();
        }

        /// <summary>
        /// Gets shortcuts that were successfully repaired
        /// </summary>
        /// <returns>List of repaired shortcuts</returns>
        public List<ShortcutValidationResult> GetRepairedShortcuts()
        {
            return Results.Where(r => r.WasRepaired).ToList();
        }
    }

    /// <summary>
    /// Represents the validation result for a single shortcut
    /// </summary>
    public class ShortcutValidationResult
    {
        /// <summary>
        /// Gets or sets the shortcut configuration that was validated
        /// </summary>
        public ShortcutConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the shortcut is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets whether the shortcut was repaired during validation
        /// </summary>
        public bool WasRepaired { get; set; }

        /// <summary>
        /// Gets or sets validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets validation information
        /// </summary>
        public List<string> Information { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the shortcut file exists
        /// </summary>
        public bool ShortcutExists { get; set; }

        /// <summary>
        /// Gets or sets whether the target executable exists
        /// </summary>
        public bool TargetExists { get; set; }

        /// <summary>
        /// Gets or sets whether the icon file exists
        /// </summary>
        public bool IconExists { get; set; }

        /// <summary>
        /// Gets or sets whether the associated profile exists
        /// </summary>
        public bool ProfileExists { get; set; }

        /// <summary>
        /// Gets or sets the path to the shortcut file
        /// </summary>
        public string? ShortcutPath { get; set; }

        /// <summary>
        /// Gets or sets when the validation was performed
        /// </summary>
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;

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
        /// <param name="configuration">Shortcut configuration</param>
        /// <param name="shortcutPath">Path to the shortcut file</param>
        /// <returns>Successful validation result</returns>
        public static ShortcutValidationResult Success(ShortcutConfiguration configuration, string shortcutPath)
        {
            return new ShortcutValidationResult
            {
                Configuration = configuration,
                IsValid = true,
                ShortcutPath = shortcutPath,
                ShortcutExists = true,
                TargetExists = true,
                IconExists = true,
                ProfileExists = true
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <param name="error">Primary error message</param>
        /// <returns>Failed validation result</returns>
        public static ShortcutValidationResult Failure(ShortcutConfiguration configuration, string error)
        {
            var result = new ShortcutValidationResult
            {
                Configuration = configuration,
                IsValid = false
            };
            result.AddError(error);
            return result;
        }
    }
}
