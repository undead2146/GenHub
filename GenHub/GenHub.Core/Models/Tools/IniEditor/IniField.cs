namespace GenHub.Core.Models.Tools.IniEditor;

/// <summary>
/// A single key/value field of an INI block. Order is significant and preserved.
/// </summary>
/// <param name="Key">The field key.</param>
/// <param name="Value">The raw field value.</param>
public sealed record IniField(string Key, string Value);
