namespace GenHub.Core;

public interface IGameInstallation
{
    public GameInstallationType InstallationType { get; }
    public bool IsVanillaInstalled { get; }
    public string VanillaGamePath { get; }
    public bool IsZeroHourInstalled { get; }
    public string ZeroHourGamePath { get; }

    public void Fetch();
}