namespace GenHub.Core;

public interface IGameDetector
{
    public bool IsVanillaInstalled { get; }
    public string VanillaGamePath { get; }
    public bool IsZeroHourInstalled { get; }
    public string ZeroHourGamePath { get; }

    public void Detect();
}