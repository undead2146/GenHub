using GenHub.Core.Interfaces;
using GenHub.Core.Helpers;
using GenHub.Core.Models;

namespace GenHub.Core.Interfaces;
public interface IGameInstallation
{
    public GameInstallationType InstallationType { get; }
    public bool IsVanillaInstalled { get; }
    public string VanillaGamePath { get; }
    public bool IsZeroHourInstalled { get; }
    public string ZeroHourGamePath { get; }

}
