using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GenHub.Core.Interfaces.AppUpdate;
using Microsoft.Extensions.Logging;

namespace GenHub.Features.AppUpdate.Services
{
    /// <summary>
    /// Implementation of IVersionComparator that compares semantic versions.
    /// </summary>
    public class SemVerComparator : IVersionComparator
    {
        private readonly ILogger<SemVerComparator> _logger;
        
        public SemVerComparator(ILogger<SemVerComparator> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Compares two version strings.
        /// </summary>
        /// <returns>
        /// Negative value if versionA is less than versionB
        /// Zero if versionA equals versionB
        /// Positive value if versionA is greater than versionB
        /// </returns>
        public int Compare(string versionA, string versionB)
        {
            try
            {
                // Normalize version strings (remove 'v' prefix if present)
                versionA = (versionA ?? "0.0.0").TrimStart('v');
                versionB = (versionB ?? "0.0.0").TrimStart('v');
                
                // Log at debug level to avoid excessive logging
                _logger.LogDebug("Comparing versions: '{VersionA}' and '{VersionB}'", versionA, versionB);
                
                // Parse versions into components - move detailed logging to debug level
                var componentsA = ParseVersionComponents(versionA);
                var componentsB = ParseVersionComponents(versionB);
                
                // Compare numeric parts first (major.minor.patch.build)
                for (int i = 0; i < Math.Min(componentsA.NumericParts.Count, componentsB.NumericParts.Count); i++)
                {
                    int result = componentsA.NumericParts[i].CompareTo(componentsB.NumericParts[i]);
                    if (result != 0)
                        return result;
                }
                
                // If one has more numeric parts than the other, the one with more parts is greater
                // For example: 1.0.0 < 1.0.0.1
                if (componentsA.NumericParts.Count != componentsB.NumericParts.Count)
                {
                    return componentsA.NumericParts.Count.CompareTo(componentsB.NumericParts.Count);
                }
                
                // If we get here, numeric parts are equal. Check pre-release identifiers
                // Pre-release versions are lower than normal versions: 1.0.0-alpha < 1.0.0
                if (componentsA.HasPreRelease && !componentsB.HasPreRelease)
                    return -1;
                
                if (!componentsA.HasPreRelease && componentsB.HasPreRelease)
                    return 1;
                
                // Both have pre-release or neither has pre-release
                // If neither has pre-release, they're equal
                if (!componentsA.HasPreRelease && !componentsB.HasPreRelease)
                    return 0;
                
                // Compare pre-release identifiers
                return ComparePreReleaseComponents(componentsA.PreReleaseParts, componentsB.PreReleaseParts);
            }
            catch (Exception ex)
            {
                // Log error but keep it brief
                _logger.LogError(ex, "Error comparing versions");
                
                // Fall back to string comparison in case of error
                return string.Compare(versionA, versionB, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Compares pre-release version components
        /// </summary>
        private int ComparePreReleaseComponents(List<string> preReleaseA, List<string> preReleaseB)
        {
            for (int i = 0; i < Math.Min(preReleaseA.Count, preReleaseB.Count); i++)
            {
                // If both are numeric
                if (int.TryParse(preReleaseA[i], out int numA) && 
                    int.TryParse(preReleaseB[i], out int numB))
                {
                    int result = numA.CompareTo(numB);
                    if (result != 0)
                        return result;
                }
                // If only one is numeric, numeric comes first
                else if (int.TryParse(preReleaseA[i], out _))
                    return -1;
                else if (int.TryParse(preReleaseB[i], out _))
                    return 1;
                // Otherwise compare lexically
                else
                {
                    int result = string.Compare(preReleaseA[i], preReleaseB[i], StringComparison.OrdinalIgnoreCase);
                    if (result != 0)
                        return result;
                }
            }
            
            // If one has more pre-release parts, it's greater
            return preReleaseA.Count.CompareTo(preReleaseB.Count);
        }
        
        /// <summary>
        /// Parses a version string into its components
        /// </summary>
        private VersionComponents ParseVersionComponents(string version)
        {
            // Default for invalid format
            if (string.IsNullOrEmpty(version))
            {
                return new VersionComponents
                {
                    NumericParts = new List<int> { 0, 0, 0 },
                    HasPreRelease = false,
                    PreReleaseParts = new List<string>()
                };
            }
            
            // Split into version and pre-release parts
            string[] versionParts = version.Split(new[] { '-' }, 2);
            
            // Parse numeric part (major.minor.patch.build)
            var numericParts = versionParts[0]
                .Split('.')
                .Select(part => int.TryParse(part, out int num) ? num : 0)
                .ToList();
            
            // Ensure we have at least 3 parts (major.minor.patch)
            while (numericParts.Count < 3)
                numericParts.Add(0);
            
            // Parse pre-release part if exists
            bool hasPreRelease = versionParts.Length > 1;
            var preReleaseParts = hasPreRelease
                ? versionParts[1].Split(new[] { '.', '-', '+' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();
            
            return new VersionComponents
            {
                NumericParts = numericParts,
                HasPreRelease = hasPreRelease,
                PreReleaseParts = preReleaseParts
            };
        }
        
        /// <summary>
        /// Helper class to store parsed version components
        /// </summary>
        private class VersionComponents
        {
            public List<int> NumericParts { get; set; } = new List<int>();
            public bool HasPreRelease { get; set; }
            public List<string> PreReleaseParts { get; set; } = new List<string>();
        }
    }
}
