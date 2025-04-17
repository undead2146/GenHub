using GenHub.Core;

namespace GenHub.Services;

public class GameDetectionService(IGameDetector gameDetector)
{
    public bool IsVanillaInstalled => gameDetector.IsVanillaInstalled;
    public string VanillaGamePath => gameDetector.VanillaGamePath;
    public bool IsZeroHourInstalled => gameDetector.IsZeroHourInstalled;
    public string ZerHourGamePath => gameDetector.ZeroHourGamePath;

    public void DetectGames()
    {
        gameDetector.Detect();
    }
}