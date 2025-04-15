using GenHub.Core;

namespace GenHub.Services;

public class GameDetectionService(IGameDetector gameDetector)
{
    public string GamePath => gameDetector.GamePath;
}