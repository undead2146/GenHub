namespace GenHub.Core.Messages;

/// <summary>
/// Message sent when the list of profiles has been significantly updated (e.g. bulk delete, bulk create).
/// Recipients should refresh the entire list.
/// </summary>
public class ProfileListUpdatedMessage
{
}
