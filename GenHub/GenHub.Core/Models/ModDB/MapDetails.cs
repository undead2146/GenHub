using GenHub.Core.Models.Enums;

namespace GenHub.Core.Models.ModDB;

/// <summary>
/// Represents detailed information about a ModDB content item parsed from a detail page.
/// Used internally by the resolver.
/// </summary>
/// <param name="name">Content name.</param>
/// <param name="description">Full description.</param>
/// <param name="author">Author/creator name.</param>
/// <param name="previewImage">Main preview image URL.</param>
/// <param name="screenshots">List of screenshot URLs.</param>
/// <param name="fileSize">File size in bytes.</param>
/// <param name="downloadCount">Number of downloads.</param>
/// <param name="submissionDate">Date submitted/released.</param>
/// <param name="downloadUrl">Direct download URL.</param>
/// <param name="targetGame">Target game type.</param>
/// <param name="contentType">Mapped content type.</param>
/// <param name="fileType">File extension/type (optional, CNCLabs-specific).</param>
/// <param name="rating">Content rating (optional, CNCLabs-specific).</param>
public record MapDetails(
    string name,
    string description,
    string author,
    string previewImage,
    List<string>? screenshots,
    long fileSize,
    int downloadCount,
    DateTime submissionDate,
    string downloadUrl,
    GameType targetGame,
    ContentType contentType,
    string? fileType = null,
    float? rating = null);
