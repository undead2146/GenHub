
namespace GenHub.Core.Models
{
    /// <summary>
    /// Information about a build extracted from artifact name or metadata
    /// </summary>
    public class GitHubBuild
    {
        /// <summary>
        /// Game variant (Generals, Zero Hour, etc.)
        /// </summary>
        public GameVariant GameVariant { get; set; } = GameVariant.Unknown;

        /// <summary>
        /// Compiler used for the build (vc6, win32, etc.)
        /// </summary>
        public string Compiler { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the commit hash
        /// </summary>
        public string? Commit { get; set; }
        
        /// <summary>
        /// Gets or sets the pull request number
        /// </summary>
        public int? PullRequestNumber { get; set; }
        
        /// <summary>
        /// Build configuration (debug, profile, release, etc.)
        /// </summary>
        public string Configuration { get; set; } = string.Empty;

        /// <summary>
        /// Version information (if available)
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Whether the 't' flag was set
        /// </summary>
        public bool HasTFlag { get; set; }

        /// <summary>
        /// Whether the 'e' flag was set
        /// </summary>
        public bool HasEFlag { get; set; }

        /// <summary>
        /// Gets string representation of this build info
        /// </summary>
        public override string ToString()
        {
            string flags = "";
            if (HasTFlag) flags += "+t";
            if (HasEFlag) flags += "+e";

            return $"{GameVariant} {Compiler} {Configuration}{flags}".Trim();
        }

        public GitHubBuild Clone()
        {
            return new GitHubBuild
            {
                GameVariant = this.GameVariant,
                Compiler = this.Compiler,
                Configuration = this.Configuration,
                Version = this.Version,
                HasTFlag = this.HasTFlag,
                HasEFlag = this.HasEFlag
            };
        }
    }

}
