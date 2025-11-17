using GenHub.Core.Models.Enums;

namespace GenHub.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="ContentType"/> enum.
/// </summary>
public static class ContentTypeExtensions
{
    /// <summary>
    /// Gets the user-friendly display name for a content type.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The display name string.</returns>
    public static string GetDisplayName(this ContentType contentType)
    {
        return contentType switch
        {
            ContentType.GameInstallation => "Game Installation",
            ContentType.GameClient => "Game Client",
            ContentType.Mod => "Modification",
            ContentType.Patch => "Patch",
            ContentType.Addon => "Add-on",
            ContentType.MapPack => "Map Pack",
            ContentType.Map => "Map",
            ContentType.Mission => "Mission",
            ContentType.LanguagePack => "Language Pack",
            ContentType.ContentBundle => "Content Bundle",
            ContentType.PublisherReferral => "Publisher Referral",
            ContentType.ContentReferral => "Content Referral",
            _ => contentType.ToString(),
        };
    }
}