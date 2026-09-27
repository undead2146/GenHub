using System.Collections.Generic;

namespace GenHub.Core.Models.Tools.IniEditor;

/// <summary>
/// An in-memory INI data document.
/// </summary>
public sealed class IniDocument
{
    /// <summary>
    /// Gets or sets the source path this document was loaded from, when known.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Gets the ordered top-level blocks.
    /// </summary>
    public List<IniBlock> Blocks { get; } = [];
}
