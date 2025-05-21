// filepath: z:\GenHub\GenHub\GenHub\Features\AppUpdate\Services\SemVerVersionComparator.cs
using System;
using System.Text.RegularExpressions;
using GenHub.Core.Interfaces.AppUpdate;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AppUpdate.Services
{
    /// <summary>
    /// Compares semantic versioning strings to determine if an update is available.
    /// </summary>
    public class SemVerVersionComparator : IVersionComparator
    {
        private readonly ILogger<SemVerVersionComparator> _logger;
        private static readonly Regex SemVerRegex = new Regex(@"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?(?:-([0-9A-Za-z-.]+))?(?:\+([0-9A-Za-z-.]+))?$", RegexOptions.Compiled);

        public SemVerVersionComparator(ILogger<SemVerVersionComparator> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Determines if a newer version is available.
        /// </summary>
        /// <param name="currentVersion">The current version of the application.</param>
        /// <param name="latestVersion">The latest version available.</param>
        /// <returns>True if an update is available; otherwise, false.</returns>
        public bool IsNewVersionAvailable(string currentVersion, string latestVersion)
        {
            return Compare(currentVersion, latestVersion) < 0;
        }

        /// <summary>
        /// Compares two version strings.
        /// </summary>
        /// <param name="currentVersion">The current version of the application.</param>
        /// <param name="latestVersion">The latest version available.</param>
        /// <returns>
        /// Less than zero: currentVersion is older than latestVersion.
        /// Zero: versions are the same.
        /// Greater than zero: currentVersion is newer than latestVersion.
        /// </returns>
        public int Compare(string currentVersion, string latestVersion)
        {
            try
            {
                _logger.LogInformation("Comparing versions: current={Current}, latest={Latest}", currentVersion, latestVersion);

                // Parse versions using a more robust method
                Version current = ParseVersion(currentVersion);
                Version latest = ParseVersion(latestVersion);

                _logger.LogDebug("Parsed versions: current={Current}, latest={Latest}", current, latest);

                // Compare the versions
                return current.CompareTo(latest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing versions: {Current} vs {Latest}", currentVersion, latestVersion);
                return 0; // Default to equal if parsing fails
            }
        }

        /// <summary>
        /// Parse a version string into a Version object
        /// </summary>
        /// <param name="versionString">The version string to parse</param>
        /// <returns>A Version object representing the parsed version</returns>
        public Version ParseVersion(string versionString)
        {
            _logger.LogDebug("Parsing version string: {Version}", versionString);
            return ParseVersionSafely(versionString);
        }

        /// <summary>
        /// Parse a version string, handling SemVer and non-standard formats
        /// </summary>
        private Version ParseVersionSafely(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
            {
                _logger.LogWarning("Version string is null or empty, returning 0.0.0");
                return new Version(0, 0, 0);
            }

            // Try to match SemVer pattern
            var match = SemVerRegex.Match(versionString);
            if (match.Success)
            {
                _logger.LogDebug("Matched SemVer pattern for {Version}", versionString);
                // Extract the numeric parts
                int major = int.Parse(match.Groups[1].Value);
                int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                int build = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

                // Create a Version object with the available components
                if (build > 0)
                {
                    return new Version(major, minor, patch, build);
                }
                return new Version(major, minor, patch);
            }

            // Fallback: Try standard Version.Parse
            try
            {
                _logger.LogDebug("Attempting standard Version.Parse for {Version}", versionString);
                return Version.Parse(versionString);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Version.Parse failed for '{Version}', returning 0.0.0", versionString);
                return new Version(0, 0, 0);
            }
        }
    }
}
