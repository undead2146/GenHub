using System;

namespace GenHub.Core.Models.Tools.RmlEditor;

/// <summary>
/// Base class for nodes of an interface document tree.
/// </summary>
public abstract class RmlNode
{
    /// <summary>
    /// Gets the stable identity of this node for editor tracking.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();
}
