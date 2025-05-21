
namespace GenHub.Core.Interfaces
{
    /// <summary>
    /// Provides a unified facade for game version operations,
    /// combining functionalities from game version management and discovery services.
    /// This interface aggregates members from <see cref="IGameVersionManager"/>
    /// and <see cref="IGameVersionDiscoveryService"/>.
    /// </summary>
    public interface    IGameVersionServiceFacade : IGameVersionManager, IGameVersionDiscoveryService
    {
    
    }
}
