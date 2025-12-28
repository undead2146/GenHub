namespace GenHub.Core.Interfaces.Steam;

/// <summary>
/// Interface for patching Steam game manifests to toggle between Steam integration and standalone launch.
/// </summary>
public interface ISteamManifestPatcher
{
    /// <summary>
    /// Patches the manifest for the specified client ID to enable or disable Steam launch logic.
    /// </summary>
    /// <param name="manifestId">The ID of the manifest to patch.</param>
    /// <param name="useSteamLaunch">If set to <c>true</c>, enables Steam launch (generals.exe); otherwise, enables standalone launch (game.dat).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PatchManifestAsync(string manifestId, bool useSteamLaunch);
}
