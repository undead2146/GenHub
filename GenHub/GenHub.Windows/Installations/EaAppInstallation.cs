using GenHub.Core;

namespace GenHub.Windows.Installations;

public class EaAppInstallation : IGameInstallation
{
    // IGameInstallation
    public GameInstallationType InstallationType => GameInstallationType.EaApp;
    public bool IsVanillaInstalled { get; }
    public string VanillaGamePath { get; }
    public bool IsZeroHourInstalled { get; }
    public string ZeroHourGamePath { get; }

    // EA App specific
    public bool IsEAAppInstalled { get; }

    public EaAppInstallation(bool fetch)
    {
        if(fetch)
            Fetch();
    }

    public void Fetch()
    {
        throw new System.NotImplementedException();
    }
}