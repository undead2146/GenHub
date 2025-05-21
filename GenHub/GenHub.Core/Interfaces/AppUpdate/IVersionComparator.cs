using System;

namespace GenHub.Core.Interfaces.AppUpdate
{
    /// <summary>
    /// Interface for comparing version strings
    /// </summary>
    public interface IVersionComparator
    {
        /// <summary>
        /// Compares two version strings
        /// </summary>
        /// <param name="versionA">First version</param>
        /// <param name="versionB">Second version</param>
        /// <returns>
        /// Negative value if versionA is less than versionB
        /// Zero if versionA equals versionB
        /// Positive value if versionA is greater than versionB
        /// </returns>
        int Compare(string versionA, string versionB);
    }
}
