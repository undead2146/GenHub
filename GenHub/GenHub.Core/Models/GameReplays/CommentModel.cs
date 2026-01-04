namespace GenHub.Core.Models.GameReplays;

/// <summary>
/// Represents a comment on a forum post.
/// </summary>
public class CommentModel
{
    /// <summary>
    /// Gets or sets the comment ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author ID.
    /// </summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author display name.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comment content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the comment was posted.
    /// </summary>
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether the comment has been edited.
    /// </summary>
    public bool IsEdited { get; set; }

    /// <summary>
    /// Gets or sets when the comment was edited.
    /// </summary>
    public DateTime? EditedAt { get; set; }
}
