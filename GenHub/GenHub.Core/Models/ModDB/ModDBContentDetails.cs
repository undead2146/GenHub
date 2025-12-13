using GenHub.Core.Models.Enums;

namespace GenHub.Core.Models.ModDB;

/// <summary>
/// Represents the detailed information about ModDB content.
/// This is an internal data structure used by the resolver and factory.
/// </summary>
public record ModDBContentDetails(
    string name,
    string description,
    string author,
    string previewImage,
    List<string>? screenshots,
    List<string>? videos,
    List<string>? articles,
    List<string>? addons,
    long fileSize,
    int downloadCount,
    DateTime submissionDate,
    string downloadUrl,
    string detailPageUrl,
    string modDBId,
    string category,
    GameType targetGame,
    ContentType contentType);
