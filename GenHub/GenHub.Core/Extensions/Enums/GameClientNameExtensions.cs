using GenHub.Core.Constants;
using GenHub.Core.Models.Enums;

namespace GenHub.Core.Extensions.Enums;

/// <summary>
/// Extension methods for GameClientName enum.
/// </summary>
public static class GameClientNameExtensions
{
    /// <summary>
    /// Gets the short display name for the game.
    /// </summary>
    /// <param name="gameName">The game name.</param>
    /// <returns>Short display name.</returns>
    public static string GetShortName(this GameClientName gameName)
    {
        return gameName switch
        {
            GameClientName.Generals => GameClientConstants.GeneralsShortName,
            GameClientName.ZeroHour => GameClientConstants.ZeroHourShortName,
            _ => gameName.ToString(),
        };
    }

    /// <summary>
    /// Gets the full display name for the game.
    /// </summary>
    /// <param name="gameName">The game name.</param>
    /// <returns>Full display name.</returns>
    public static string GetFullName(this GameClientName gameName)
    {
        return gameName switch
        {
            GameClientName.Generals => GameClientConstants.GeneralsFullName,
            GameClientName.ZeroHour => GameClientConstants.ZeroHourFullName,
            _ => gameName.ToString(),
        };
    }
}
