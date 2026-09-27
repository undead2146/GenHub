namespace GenHub.Core.Models.Tools.IniEditor;

/// <summary>
/// Describes a single INI field for schema assisted editing.
/// </summary>
/// <param name="Key">The field key.</param>
/// <param name="Description">Short description of the field.</param>
/// <param name="IsNumeric">Whether the value is numeric.</param>
public sealed record IniFieldSchema(string Key, string Description, bool IsNumeric);
