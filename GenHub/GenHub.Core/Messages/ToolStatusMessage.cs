namespace GenHub.Core.Messages;

/// <summary>
/// Message sent to show a status message in the Tools tab.
/// </summary>
/// <param name="Message">The message to display.</param>
/// <param name="IsSuccess">Whether it's a success message.</param>
/// <param name="IsError">Whether it's an error message.</param>
/// <param name="IsInfo">Whether it's an info message.</param>
public record ToolStatusMessage(string Message, bool IsSuccess = false, bool IsError = false, bool IsInfo = false);
