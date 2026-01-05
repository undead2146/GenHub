namespace GenHub.Core.Features.ActionSets;

using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.GameInstallations;

/// <summary>
/// Defines a set of actions to fix or enhance a game installation.
/// </summary>
public interface IActionSet
{
    /// <summary>
    /// Gets the unique identifier for this action set.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the title of the action set.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets a value indicating whether this is a core fix applied by default.
    /// </summary>
    bool IsCoreFix { get; }

    /// <summary>
    /// Gets a value indicating whether this is a crucial fix for game stability.
    /// </summary>
    bool IsCrucialFix { get; }

    /// <summary>
    /// Checks if the action set is applicable to the current system and game installation.
    /// </summary>
    /// <param name="installation">The game installation to check.</param>
    /// <returns>A task representing the asynchronous operation, returning true if applicable.</returns>
    Task<bool> IsApplicableAsync(GameInstallation installation);

    /// <summary>
    /// Checks if the action set has already been applied.
    /// </summary>
    /// <param name="installation">The game installation to check.</param>
    /// <returns>A task representing the asynchronous operation, returning true if applied.</returns>
    Task<bool> IsAppliedAsync(GameInstallation installation);

    /// <summary>
    /// Applies the action set patches.
    /// </summary>
    /// <param name="installation">The game installation to patch.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the action.</returns>
    Task<ActionSetResult> ApplyAsync(GameInstallation installation, CancellationToken ct = default);

    /// <summary>
    /// Undoes the action set patches if possible.
    /// </summary>
    /// <param name="installation">The game installation to revert.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, returning the result of the undo operation.</returns>
    Task<ActionSetResult> UndoAsync(GameInstallation installation, CancellationToken ct = default);
}

/// <summary>
/// Represents the result of an action set operation.
/// </summary>
public record ActionSetResult(bool Success, string? ErrorMessage = null);
