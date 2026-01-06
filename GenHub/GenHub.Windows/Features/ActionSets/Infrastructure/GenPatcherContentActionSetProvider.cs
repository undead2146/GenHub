namespace GenHub.Windows.Features.ActionSets.Infrastructure;

using System.Collections.Generic;
using GenHub.Core.Features.ActionSets;
using GenHub.Core.Models.CommunityOutpost;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provider for action sets based on GenPatcher content metadata.
/// </summary>
public class GenPatcherContentActionSetProvider(ILoggerFactory loggerFactory) : IActionSetProvider
{

    /// <inheritdoc/>
    public IEnumerable<IActionSet> GetActionSets()
    {
        var codes = GenPatcherContentRegistry.GetKnownContentCodes();

        foreach (var code in codes)
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

            var logger = this.loggerFactory.CreateLogger<ContentActionSet>();
            yield return new ContentActionSet(metadata, logger);
        }
    }
}
