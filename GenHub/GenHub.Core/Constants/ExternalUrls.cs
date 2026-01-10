namespace GenHub.Core.Constants;

/// <summary>
/// Constants for external URLs used for downloading dependencies or tools.
/// </summary>
public static class ExternalUrls
{
    /// <summary>
    /// Download URL for Visual C++ 2010 Redistributable Package (x86).
    /// Required for Generals and Zero Hour to run.
    /// </summary>
    public const string VCRedist2010DownloadUrl = "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe";

    /// <summary>
    /// Download URL for DirectX 8.1 / 9.0c runtime files used by GenPatcher.
    /// </summary>
    public const string DirectXRuntimeDownloadUrl = "https://gentool.net/program_data/genpatcher/drtx.dat";

    /// <summary>
    /// Download URL for Generals 1.08 official patch.
    /// </summary>
    public const string Generals108PatchUrl = "https://gentool.net/program_data/genpatcher/10gn.dat";

    /// <summary>
    /// Download URL for Zero Hour 1.04 official patch.
    /// </summary>
    public const string ZeroHour104PatchUrl = "https://gentool.net/program_data/genpatcher/10zh.dat";
}
