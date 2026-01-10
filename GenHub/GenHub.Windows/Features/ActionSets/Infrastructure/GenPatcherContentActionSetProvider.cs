using System.Collections.Generic;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.CommunityOutpost;
using Microsoft.Extensions.Logging;

namespace GenHub.Windows.Features.ActionSets.Infrastructure;

/// <summary>
/// Provides action sets derived from GenPatcher content (patches, etc.).
/// Filters out content that belongs in the Downloads tab.
/// </summary>
public class GenPatcherContentActionSetProvider : IActionSetProvider
{
    private readonly ILogger<ContentActionSet> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenPatcherContentActionSetProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger factory.</param>
    public GenPatcherContentActionSetProvider(ILogger<ContentActionSet> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IEnumerable<IActionSet> GetActionSets()
    {
        var contentCodes = GenPatcherContentRegistry.GetKnownContentCodes();

        foreach (var code in contentCodes)
        {
            var metadata = GenPatcherContentRegistry.GetMetadata(code);

            // Filter out content that shouldn't appear in the "Tools" tab fix list.
            // We only want to show actual Game Patches and Community Patches here.
            // Addons like Maps, Control Bars, Hotkeys, etc. are handled via the Downloads tab.
            if (metadata.Category == GenPatcherContentCategory.ControlBar ||
                metadata.Category == GenPatcherContentCategory.Camera ||
                metadata.Category == GenPatcherContentCategory.Hotkeys ||
                metadata.Category == GenPatcherContentCategory.Tools ||
                metadata.Category == GenPatcherContentCategory.Maps ||
                metadata.Category == GenPatcherContentCategory.Visuals)
            {
                continue;
            }

            yield return new ContentActionSet(metadata, _logger);
        }
    }
}
