namespace GenHub.Core.Messages;

/// <summary>
/// Message sent when the application theme has changed.
/// </summary>
/// <param name="ThemeName">Name of the new theme.</param>
public record ThemeChangedMessage(string ThemeName);
