namespace GenHub.Core.Models
{
    /// <summary>
    /// Defines types of game installations for tracking installation sources
    /// </summary>
    public enum GameInstallationType
    {
        Unknown = 0,
        Steam = 1,
        EaApp = 2,
        Origin = 3,
        TheFirstDecade = 4,
        RGMechanics = 5,
        CDISO = 6,
        GitHubArtifact = 7,
        GitHubRelease = 8,
        LocalZipFile = 9,
        DirectoryImport = 10,
        Custom = 11,
    }
}
