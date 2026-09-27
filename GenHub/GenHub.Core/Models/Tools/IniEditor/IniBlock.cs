using System.Collections.Generic;

namespace GenHub.Core.Models.Tools.IniEditor;

/// <summary>
/// A single INI block (for example <c>Object ... End</c>).
/// </summary>
public sealed class IniBlock
{
    /// <summary>
    /// Gets or sets the block type (for example <c>Object</c> or <c>Weapon</c>).
    /// </summary>
    public string BlockType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the block name or identifier tokens from the opening line.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the ordered fields of this block.
    /// </summary>
    public List<IniField> Fields { get; } = [];

    /// <summary>
    /// Gets nested module sub-blocks (for example Behavior or Draw modules inside an Object).
    /// </summary>
    public List<IniBlock> Children { get; } = [];

    /// <summary>
    /// Gets or sets the 1-based line number where the block starts, when known.
    /// </summary>
    public int LineNumber { get; set; }
}
