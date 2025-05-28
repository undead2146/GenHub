using System;
using GenHub.Core.Models;

namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents build information parsed from GitHub artifact names
    /// </summary>
    public class GitHubBuild
    {
        /// <summary>
        /// Gets or sets the game variant (Generals, ZeroHour, etc.)
        /// </summary>
        public GameVariant GameVariant { get; set; } = GameVariant.Unknown;

        /// <summary>
        /// Gets or sets the compiler used (vc6, win32, etc.)
        /// </summary>
        public string Compiler { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets the build configuration (debug, release, etc.)
        /// </summary>
        public string Configuration { get; set; } = "Unknown";

        /// <summary>
        /// Gets or sets whether the T flag is present
        /// </summary>
        public bool HasTFlag { get; set; }

        /// <summary>
        /// Gets or sets whether the E flag is present
        /// </summary>
        public bool HasEFlag { get; set; }

        /// <summary>
        /// Gets or sets the version string for this build
        /// </summary>
        public string Version { get; set; } = "Unknown";

        /// <summary>
        /// Gets a display string for the build configuration
        /// </summary>
        public string DisplayName
        {
            get
            {
                var flags = string.Empty;
                if (HasTFlag) flags += "+T";
                if (HasEFlag) flags += "+E";
                
                return $"{GameVariant} {Compiler} {Configuration}{flags}";
            }
        }

        /// <summary>
        /// Creates a deep copy of this GitHubBuild instance
        /// </summary>
        public GitHubBuild Clone()
        {
            return new GitHubBuild
            {
                GameVariant = this.GameVariant,
                Compiler = this.Compiler,
                Configuration = this.Configuration,
                HasTFlag = this.HasTFlag,
                HasEFlag = this.HasEFlag,
                Version = this.Version
            };
        }

        /// <summary>
        /// Returns a string representation of the build info
        /// </summary>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
