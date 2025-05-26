using GenHub.Core.Models.Enums;

namespace GenHub.Core.Models.GameProfiles
{
    /// <summary>
    /// Domain model representing detailed information about a game executable file
    /// </summary>
    public class GameExecutableInfo
    {
        /// <summary>
        /// Full path to the executable file
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Type of game (e.g., "Generals", "Zero Hour")
        /// </summary>
        public string GameType { get; set; } = string.Empty;

        /// <summary>
        /// Whether this executable is for Zero Hour expansion
        /// </summary>
        public bool IsZeroHour { get; set; }

        /// <summary>
        /// Whether the executable file is valid and can be used
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Root installation directory containing the executable
        /// </summary>
        public string InstallPath { get; set; } = string.Empty;

        /// <summary>
        /// Source type indicating where this installation came from
        /// </summary>
        public GameInstallationType SourceType { get; set; }

        /// <summary>
        /// File name of the executable (derived from ExecutablePath)
        /// </summary>
        public string ExecutableFileName => string.IsNullOrEmpty(ExecutablePath) 
            ? string.Empty 
            : System.IO.Path.GetFileName(ExecutablePath);

        /// <summary>
        /// Whether this executable represents a retail installation
        /// </summary>
        public bool IsRetailInstallation => SourceType is 
            GameInstallationType.Steam or 
            GameInstallationType.EaApp or 
            GameInstallationType.Origin or 
            GameInstallationType.TheFirstDecade;

        /// <summary>
        /// Whether this executable comes from a GitHub build
        /// </summary>
        public bool IsGitHubBuild => SourceType is 
            GameInstallationType.GitHubArtifact or 
            GameInstallationType.GitHubRelease;

        /// <summary>
        /// Display name for the source type
        /// </summary>
        public string SourceDisplayName => SourceType switch
        {
            GameInstallationType.Steam => "Steam",
            GameInstallationType.EaApp => "EA App",
            GameInstallationType.Origin => "Origin",
            GameInstallationType.TheFirstDecade => "The First Decade",
            GameInstallationType.GitHubArtifact => "GitHub Artifact",
            GameInstallationType.GitHubRelease => "GitHub Release",
            GameInstallationType.LocalZipFile => "Local Installation",
            GameInstallationType.DirectoryImport => "Imported Directory",
            _ => "Custom Installation"
        };

        /// <summary>
        /// Creates a GameVersion from this executable info
        /// </summary>
        public GameVersion ToGameVersion()
        {
            return new GameVersion
            {
                Id = System.Guid.NewGuid().ToString(),
                ExecutablePath = ExecutablePath,
                InstallPath = InstallPath,
                SourceType = SourceType,
                IsZeroHour = IsZeroHour,
                GameType = GameType,
                Name = $"{SourceDisplayName} - {GameType}"
            };
        }
    }
}
